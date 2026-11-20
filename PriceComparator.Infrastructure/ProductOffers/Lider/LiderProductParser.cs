
using System.Globalization;
using System.Text.Json;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Infrastructure.ProductOffers.Lider;

public sealed class LiderProductParser : ISingletonDependency
{
    private static readonly Uri BaseUri = new("https://super.lider.cl");

    public (string RawJson, IReadOnlyCollection<Product> Products) ParseGraphQL(string json, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!TryObject(root, "data", out var data) ||
            !TryObject(data, "search", out var search) ||
            !TryObject(search, "searchResult", out var result))
        {
            throw new InvalidOperationException(
                "[LIDER] Respuesta GraphQL sin data.search.searchResult.");
        }

        if (!result.TryGetProperty("itemStacks", out var stacks) ||
            stacks.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "[LIDER] itemStacks no es un arreglo.");
        }

        var raw = new List<JsonElement>();
        var products = new List<Product>();
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var stack in stacks.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (stack.ValueKind != JsonValueKind.Object ||
                IsSponsored(stack))
                continue;

            if (!stack.TryGetProperty("itemsV2", out var items) ||
                items.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in items.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (item.ValueKind != JsonValueKind.Object ||
                    GetString(item, "__typename") != "Product")
                    continue;

                raw.Add(item.Clone());

                var product = CreateProduct(item);

                if (product is not null &&
                    urls.Add(product.ProductUrl.AbsoluteUri))
                {
                    products.Add(product);
                }
            }
        }

        return (JsonSerializer.Serialize(raw), products);
    }

    private static Product? CreateProduct(JsonElement item)
    {
        var name = GetString(item, "name");
        var url = GetUri(item, "canonicalUrl");
        var slug = url?.ToString().Trim('/').Split('/')[^2];

        if (string.IsNullOrWhiteSpace(name) || url is null)
            return null;

        decimal price = 0;

        if (TryObject(item, "priceInfo", out var priceInfo) &&
            TryObject(priceInfo, "currentPrice", out var currentPrice))
        {
            price = GetDecimal(currentPrice, "price");
        }

        if (price <= 0)
            price = GetDecimal(item, "price");

        if (price <= 0)
            return null;

        Uri? imageUrl = GetUri(item, "image");

        if (imageUrl is null &&
            TryObject(item, "imageInfo", out var imageInfo))
        {
            imageUrl = GetUri(imageInfo, "thumbnailUrl");
        }

        return new Product(
            name: name,
            price: price,
            store: new Store { Code = "LIDER", Name = "Lider" },
            productUrl: url,
            imageUrl: imageUrl,
            brand: GetString(item, "brand"),
            sellerName: GetString(item, "sellerName"),
            categoriesStore: GetCategories(item),
            availability: GetAvailability(item),
            slug);
    }

    private static bool IsSponsored(JsonElement stack)
    {
        return TryObject(stack, "meta", out var meta) &&
               meta.TryGetProperty("isSponsored", out var value) &&
               value.ValueKind == JsonValueKind.True;
    }

    private static IReadOnlyCollection<string> GetCategories(JsonElement item)
    {
        if (!TryObject(item, "category", out var category))
            return [];

        var categories = new List<string>();

        if (category.TryGetProperty("path", out var path))
        {
            if (path.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in path.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object)
                        continue;

                    var name = GetString(entry, "name");

                    if (!string.IsNullOrWhiteSpace(name))
                        categories.Add(name);
                }
            }
            else if (path.ValueKind == JsonValueKind.Object)
            {
                var name = GetString(path, "name");

                if (!string.IsNullOrWhiteSpace(name))
                    categories.Add(name);
            }
        }

        var categoryPath = GetString(category, "categoryPath");

        if (!string.IsNullOrWhiteSpace(categoryPath))
        {
            categories.AddRange(categoryPath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries));
        }

        return categories
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? GetAvailability(JsonElement item)
    {
        var status = GetString(item, "availabilityStatus");

        if (!string.IsNullOrWhiteSpace(status))
            return status;

        if (TryObject(item, "availabilityStatusV2", out var v2))
        {
            status = GetString(v2, "value");

            if (!string.IsNullOrWhiteSpace(status))
                return status;
        }

        return GetString(item, "availabilityStatusDisplayValue") switch
        {
            "In stock" => "IN_STOCK",
            "Out of stock" => "OUT_OF_STOCK",
            var value => value
        };
    }

    private static bool TryObject(
        JsonElement element,
        string name,
        out JsonElement value)
    {
        value = default;

        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(name, out value) &&
               value.ValueKind == JsonValueKind.Object;
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.String)
            return null;

        return value.GetString()?.Trim();
    }

    private static decimal GetDecimal(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var value))
            return 0;

        if (value.ValueKind == JsonValueKind.Number)
            return value.TryGetDecimal(out var number) ? number : 0;

        if (value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(
                value.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed))
            return parsed;

        return 0;
    }

    private static Uri? GetUri(JsonElement element, string name)
    {
        var value = GetString(element, name);

        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
            return absolute;

        return Uri.TryCreate(BaseUri, value, out var relative)
            ? relative
            : null;
    }
}
