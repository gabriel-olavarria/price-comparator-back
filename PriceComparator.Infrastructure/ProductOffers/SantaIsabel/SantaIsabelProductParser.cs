
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Infrastructure.ProductOffers.SantaIsabel;

public sealed class SantaIsabelProductParser : ISingletonDependency
{
    private static readonly Uri SantaIsabelBaseUri =
        new("https://www.santaisabel.cl");

    public async Task<IReadOnlyCollection<Product>> ParseAsync(
        string html,
        CancellationToken cancellationToken = default)
    {
        var rawJson = await ExtractRawJsonAsync(
            html,
            cancellationToken);

        return ParseRawProducts(rawJson);
    }

    public async Task<string> ExtractRawJsonAsync(
        string html,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "[]";

        var htmlParser = new HtmlParser();

        var document = await htmlParser.ParseDocumentAsync(
            html,
            cancellationToken);

        var categoriesByUrl = GetCategoriesByProductUrl(document);
        var categoriesById = GetCategoriesByProductId(document);

        var products = new JsonArray();
        var productIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var scripts = document.QuerySelectorAll("script")
            .Where(script => script.TextContent.Contains(
                "self.__next_f.push(",
                StringComparison.Ordinal));

        foreach (var script in scripts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scriptContent = script.TextContent;

            const string marker = "self.__next_f.push(";

            var position = 0;

            while ((position = scriptContent.IndexOf(
                       marker,
                       position,
                       StringComparison.Ordinal)) >= 0)
            {
                var start = position + marker.Length;

                if (!TryReadJsonValue(
                        scriptContent.AsSpan(start),
                        out var pushDocument,
                        out var consumed))
                {
                    position = start;
                    continue;
                }

                position = start + consumed;

                using (pushDocument)
                {
                    var root = pushDocument.RootElement;

                    if (root.ValueKind != JsonValueKind.Array ||
                        root.GetArrayLength() < 2 ||
                        root[1].ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var payload = root[1].GetString();

                    if (string.IsNullOrEmpty(payload))
                        continue;

                    ExtractProductsFromPayload(
                        payload,
                        products,
                        productIds,
                        categoriesByUrl,
                        categoriesById,
                        cancellationToken);
                }
            }
        }

        Console.WriteLine(
            $"[SANTA ISABEL][RAW] Productos extraídos: {products.Count}");

        return products.ToJsonString(
            new JsonSerializerOptions
            {
                WriteIndented = true
            });
    }

    private static void ExtractProductsFromPayload(
        string payload,
        JsonArray products,
        HashSet<string> productIds,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> categoriesByUrl,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> categoriesById,
        CancellationToken cancellationToken)
    {
        const string productMarker = "\"product\":{";

        var searchPosition = 0;

        while ((searchPosition = payload.IndexOf(
                   productMarker,
                   searchPosition,
                   StringComparison.Ordinal)) >= 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var objectStart =
                searchPosition + "\"product\":".Length;

            if (!TryReadJsonValue(
                    payload.AsSpan(objectStart),
                    out var productDocument,
                    out var productLength))
            {
                searchPosition = objectStart + 1;
                continue;
            }

            searchPosition = objectStart + productLength;

            using (productDocument)
            {
                var product = productDocument.RootElement;

                if (!TryGetString(
                        product,
                        "productId",
                        out var productId))
                {
                    continue;
                }

                if (!productIds.Add(productId))
                    continue;

                // Clonamos el objeto completo, sin eliminar
                // ni transformar sus propiedades originales.
                var productNode = JsonNode.Parse(
                    product.GetRawText())!.AsObject();

                IReadOnlyCollection<string>? categories = null;

                // 1. Intentar encontrar categorías por URL.
                if (TryGetString(product, "slug", out var slug))
                {
                    var path = NormalizeProductPath(
                        $"/{slug}/p");

                    categoriesByUrl.TryGetValue(
                        path,
                        out categories);
                }

                // 2. Intentar por productId.
                if (categories is null)
                {
                    categoriesById.TryGetValue(
                        productId,
                        out categories);
                }

                // 3. Intentar por referencia comercial.
                if (categories is null &&
                    TryGetString(
                        product,
                        "reference",
                        out var reference))
                {
                    categoriesById.TryGetValue(
                        reference,
                        out categories);
                }

                // 4. Conservar categoryNames original
                // y agregar las categorías resueltas.
                if (categories is { Count: > 0 })
                {
                    productNode["resolvedCategoryNames"] =
                        new JsonArray(
                            categories.Select(category =>
                                (JsonNode?)JsonValue.Create(category))
                                .ToArray());
                }

                products.Add(productNode);
            }
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyCollection<string>>
        GetCategoriesByProductUrl(IDocument document)
    {
        var result =
            new Dictionary<string, IReadOnlyCollection<string>>(
                StringComparer.OrdinalIgnoreCase);

        var productLinks = document.QuerySelectorAll(
            "a[data-gtm-product-click][href]");

        foreach (var link in productLinks)
        {
            var href = link.GetAttribute("href");

            if (string.IsNullOrWhiteSpace(href))
                continue;

            var categories = ReadCategoriesFromAnalytics(link);

            if (categories.Count == 0)
                continue;

            result[NormalizeProductPath(href)] = categories;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyCollection<string>>
        GetCategoriesByProductId(IDocument document)
    {
        var result =
            new Dictionary<string, IReadOnlyCollection<string>>(
                StringComparer.OrdinalIgnoreCase);

        var productElements = document.QuerySelectorAll(
            "[data-gtm-product-click]");

        foreach (var element in productElements)
        {
            var analyticsJson = element.GetAttribute(
                "data-gtm-product-click");

            if (string.IsNullOrWhiteSpace(analyticsJson))
                continue;

            try
            {
                using var analyticsDocument = JsonDocument.Parse(
                    WebUtility.HtmlDecode(analyticsJson));

                var root = analyticsDocument.RootElement;

                if (!TryGetString(
                        root,
                        "category",
                        out var categoryPath))
                {
                    continue;
                }

                var categories = SplitCategories(categoryPath);

                if (categories.Count == 0)
                    continue;

                if (TryGetString(root, "id", out var id))
                    result[id] = categories;

                if (TryGetString(root, "refId", out var refId))
                    result[refId] = categories;
            }
            catch (JsonException)
            {
                // Ignorar atributos mal formados.
            }
        }

        return result;
    }

    private static IReadOnlyCollection<string> ReadCategoriesFromAnalytics(
        IElement element)
    {
        var analyticsJson = element.GetAttribute(
            "data-gtm-product-click");

        if (string.IsNullOrWhiteSpace(analyticsJson))
            return [];

        try
        {
            using var analyticsDocument = JsonDocument.Parse(
                WebUtility.HtmlDecode(analyticsJson));

            if (!TryGetString(
                    analyticsDocument.RootElement,
                    "category",
                    out var categoryPath))
            {
                return [];
            }

            return SplitCategories(categoryPath);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyCollection<string> SplitCategories(
        string categoryPath)
    {
        return categoryPath
            .Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Where(category =>
                !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyCollection<Product> ParseRawProducts(
        string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return [];

        using var document = JsonDocument.Parse(rawJson);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        var offers = new List<Product>();

        foreach (var product in document.RootElement.EnumerateArray())
        {
            if (product.ValueKind != JsonValueKind.Object)
                continue;

            var offer = TryCreateProduct(product);

            if (offer is not null)
                offers.Add(offer);
        }

        Console.WriteLine(
            $"[SANTA ISABEL][PROCESSED] Productos válidos: {offers.Count}");

        return offers
            .GroupBy(
                product => product.ProductUrl.AbsoluteUri,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static Product? TryCreateProduct(JsonElement product)
    {
        if (!TryGetString(product, "slug", out var slug))
            return null;

        if (!product.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        TryGetString(product, "brand", out var brand);
        TryGetString(product, "soldBy", out var sellerName);

        var categories = GetCategories(product);

        var productUrl = new Uri(
            SantaIsabelBaseUri,
            $"/{slug.Trim('/')}/p");

        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            if (!TryGetString(item, "name", out var name) &&
                !TryGetString(product, "productName", out name) &&
                !TryGetString(product, "name", out name))
            {
                continue;
            }

            if (!TryGetDecimal(item, "price", out var price) ||
                price <= 0)
            {
                continue;
            }

            var imageUrl = GetImageUrl(item);
            var availability = GetAvailability(item);

            return new Product(
                name: name,
                price: price,
                store: new Store
                {
                    Code = "SANTA_ISABEL",
                    Name = "Santa Isabel"
                },
                productUrl: productUrl,
                imageUrl: imageUrl,
                brand: string.IsNullOrWhiteSpace(brand)
                    ? null
                    : brand,
                sellerName: string.IsNullOrWhiteSpace(sellerName)
                    ? "Santa Isabel"
                    : sellerName,
                categoriesStore: categories,
                availability: availability);
        }

        return null;
    }

    private static IReadOnlyCollection<string> GetCategories(
        JsonElement product)
    {
        // Prioridad: categorías legibles obtenidas del HTML.
        if (product.TryGetProperty(
                "resolvedCategoryNames",
                out var resolved) &&
            resolved.ValueKind == JsonValueKind.Array)
        {
            return ReadCategoryArray(resolved);
        }

        // Alternativa: categoryNames ya viene como arreglo.
        if (product.TryGetProperty(
                "categoryNames",
                out var categoryNames) &&
            categoryNames.ValueKind == JsonValueKind.Array)
        {
            return ReadCategoryArray(categoryNames);
        }

        return [];
    }

    private static IReadOnlyCollection<string> ReadCategoryArray(
        JsonElement categories)
    {
        return categories
            .EnumerateArray()
            .Where(value =>
                value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString()!)
            .Where(value =>
                !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Uri? GetImageUrl(JsonElement item)
    {
        if (!item.TryGetProperty("images", out var images) ||
            images.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var image in images.EnumerateArray())
        {
            string? imageUrlText = null;

            if (image.ValueKind == JsonValueKind.String)
            {
                imageUrlText = image.GetString();
            }
            else if (image.ValueKind == JsonValueKind.Object &&
                     TryGetString(
                         image,
                         "imageUrl",
                         out var url))
            {
                imageUrlText = url;
            }

            if (Uri.TryCreate(
                    imageUrlText,
                    UriKind.Absolute,
                    out var imageUrl))
            {
                return imageUrl;
            }
        }

        return null;
    }

    private static string? GetAvailability(JsonElement item)
    {
        if (!item.TryGetProperty("stock", out var stock))
            return null;

        return stock.ValueKind switch
        {
            JsonValueKind.True => "IN_STOCK",
            JsonValueKind.False => "OUT_OF_STOCK",
            _ => null
        };
    }

    private static bool TryReadJsonValue(
        ReadOnlySpan<char> input,
        out JsonDocument document,
        out int consumed)
    {
        document = null!;
        consumed = 0;

        try
        {
            var bytes = Encoding.UTF8.GetBytes(
                input.ToString());

            var reader = new Utf8JsonReader(bytes);

            document = JsonDocument.ParseValue(ref reader);

            consumed = Encoding.UTF8.GetCharCount(
                bytes.AsSpan(
                    0,
                    (int)reader.BytesConsumed));

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string NormalizeProductPath(string url)
    {
        if (Uri.TryCreate(
                url,
                UriKind.Absolute,
                out var absoluteUri))
        {
            return absoluteUri.AbsolutePath.TrimEnd('/');
        }

        var path = url.Split('?')[0];

        if (!path.StartsWith('/'))
            path = $"/{path}";

        return path.TrimEnd('/');
    }

    private static bool TryGetString(
        JsonElement element,
        string propertyName,
        out string value)
    {
        value = string.Empty;

        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var result = property.GetString();

        if (string.IsNullOrWhiteSpace(result))
            return false;

        value = result.Trim();

        return true;
    }

    private static bool TryGetDecimal(
        JsonElement element,
        string propertyName,
        out decimal value)
    {
        value = 0;

        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        return property.TryGetDecimal(out value);
    }
}
