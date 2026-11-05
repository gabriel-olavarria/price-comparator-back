using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.ValueObjects;
using System.Net;
using System.Text.RegularExpressions;

namespace PriceComparator.Infrastructure.ProductOffers.Acuenta;

public sealed class AcuentaProductParser : ISingletonDependency
{
    private const string Marker = "self.__next_f.push(";
    private static readonly Uri BaseUri = new("https://www.acuenta.cl/");
    private static readonly Regex RecordLine = new(@"^([0-9a-fA-F]+):(.*)$", RegexOptions.Compiled);

    public async Task<IReadOnlyCollection<Product>> ParseAsync(string html, CancellationToken cancellationToken = default)
    {
        var records = await ReadRecordsAsync(html, cancellationToken);
        var products = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in records.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value.ValueKind != JsonValueKind.Object ||
                !TryString(value, "__typename", out var type) ||
                type != "CatalogProductModel" ||
                !TryString(value, "sku", out var sku) ||
                !TryString(value, "name", out var name) ||
                !TryPrice(value, out var price) || price <= 0)
                continue;

            var brand = TryString(value, "brand", out var b) ? b : null;
            var description = TryString(value, "description", out var d)
                ? d
                : null;

            var productName = GetProductName(name, description);
            
            var images = ResolveStrings(value, "photosUrl", records);
            var imageUrl = images.Select(ToHttpUri).FirstOrDefault(uri => uri is not null);
            var available = value.TryGetProperty("isAvailable", out var availableElement) &&
                            availableElement.ValueKind == JsonValueKind.True;

            // URL de búsqueda provisional: el slug no confirma por sí solo la ruta pública.
            var productUrl = new Uri(BaseUri,
                $"search?name={Uri.EscapeDataString(sku)}&currentPage=1");

            products[sku] = new Product(
                name: productName,
                price: price,
                store: new Store { Code = "ACUENTA", Name = "aCuenta" },
                productUrl: productUrl,
                imageUrl: imageUrl,
                brand: brand,
                sellerName: "aCuenta",
                categoriesStore: [],
                availability: available ? "IN_STOCK" : "OUT_OF_STOCK");
        }

        return products.Values.ToArray();
    }
    private static string GetProductName(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return name;

        var match = Regex.Match(
            description,
            @"<strong>\s*Variedad:\s*</strong>\s*([^<]+)",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            return name;

        var variety = WebUtility.HtmlDecode(match.Groups[1].Value).Trim();

        if (string.IsNullOrWhiteSpace(variety) ||
            name.Contains(variety, StringComparison.OrdinalIgnoreCase))
            return name;

        return $"{name} - {variety}";
    }
    public async Task<string> ExtractRawJsonAsync(string html, CancellationToken cancellationToken = default)
    {
        var records = await ReadRecordsAsync(html, cancellationToken);
        var raw = records
            .Where(pair => pair.Value.ValueKind == JsonValueKind.Object && TryString(pair.Value, "__typename", out var type) && type == "CatalogProductModel")
            .Select(pair => new { id = pair.Key, product = pair.Value })
            .ToArray();

        return JsonSerializer.Serialize(raw);
    }

    private static async Task<Dictionary<string, JsonElement>> ReadRecordsAsync(string html, CancellationToken cancellationToken)
    {
        var records = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(html)) 
            return records;

        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(html, cancellationToken);
        var flight = new StringBuilder();

        foreach (var script in document.QuerySelectorAll("script"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = script.TextContent;
            var position = 0;
            while ((position = source.IndexOf(Marker, position, StringComparison.Ordinal)) >= 0)
            {
                var start = position + Marker.Length;
                if (!TryReadJson(source.AsSpan(start), out var payload, out var consumed))
                {
                    position = start;
                    continue;
                }

                position = start + consumed;
                using (payload)
                {
                    var root = payload.RootElement;
                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() >= 2 &&
                        root[0].ValueKind == JsonValueKind.Number && root[0].TryGetInt32(out var kind) &&
                        kind == 1 && root[1].ValueKind == JsonValueKind.String)
                        flight.Append(root[1].GetString());
                }
            }
        }

        foreach (var line in flight.ToString().Split('\n'))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var match = RecordLine.Match(line.TrimEnd('\r'));
            if (!match.Success) continue;
            try
            {
                using var json = JsonDocument.Parse(match.Groups[2].Value);
                records[match.Groups[1].Value] = json.RootElement.Clone();
            }
            catch (JsonException) { /* Algunas líneas RSC no contienen JSON. */ }
        }
        return records;
    }

    private static IReadOnlyCollection<string> ResolveStrings(
        JsonElement product, string property,
        IReadOnlyDictionary<string, JsonElement> records)
    {
        if (!product.TryGetProperty(property, out var value)) return [];
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (value.ValueKind == JsonValueKind.String)
        {
            var reference = value.GetString();
            if (reference is null || reference.Length < 2 || reference[0] != '$' ||
                !reference.AsSpan(1).ToString().All(Uri.IsHexDigit)) return [];
            var key = reference[1..];
            if (!visited.Add(key) || !records.TryGetValue(key, out value)) return [];
        }
        if (value.ValueKind != JsonValueKind.Array) return [];
        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToArray();
    }

    private static bool TryString(JsonElement obj, string property, out string result)
    {
        result = "";
        if (!obj.TryGetProperty(property, out var value)) return false;
        if (value.ValueKind == JsonValueKind.String) result = value.GetString()?.Trim() ?? "";
        else if (value.ValueKind == JsonValueKind.Number) result = value.GetRawText();
        return result.Length > 0;
    }

    private static bool TryPrice(JsonElement obj, out decimal price)
    {
        price = 0;
        if (!obj.TryGetProperty("price", out var value)) return false;
        if (value.ValueKind == JsonValueKind.Number) return value.TryGetDecimal(out price);
        return value.ValueKind == JsonValueKind.String &&
               decimal.TryParse(value.GetString(), System.Globalization.NumberStyles.Number,
                   System.Globalization.CultureInfo.InvariantCulture, out price);
    }

    private static Uri? ToHttpUri(string value)
    {
        if (value.StartsWith("//", StringComparison.Ordinal)) value = "https:" + value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            Uri.TryCreate(BaseUri, value, out uri);
        return uri is not null && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri : null;
    }

    private static bool TryReadJson(ReadOnlySpan<char> input, out JsonDocument document, out int consumed)
    {
        document = null!;
        consumed = 0;
        try
        {
            var bytes = Encoding.UTF8.GetBytes(input.ToString());
            var reader = new Utf8JsonReader(bytes);
            document = JsonDocument.ParseValue(ref reader);
            consumed = Encoding.UTF8.GetCharCount(bytes.AsSpan(0, checked((int)reader.BytesConsumed)));
            return true;
        }
        catch (JsonException) { return false; }
    }
}
