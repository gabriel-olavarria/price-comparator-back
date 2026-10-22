using System.Globalization;
using System.Text.Json;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Infrastructure.ProductOffers.Unimarc;

public sealed class UnimarcProductJsonParser : ITransientDependency
{
    private static readonly Uri BaseUri = new("https://www.unimarc.cl");

    public IReadOnlyCollection<Product> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty(
                "availableProducts",
                out var availableProducts)
            || availableProducts.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException(
                "La respuesta de Unimarc no contiene availableProducts.");
        }

        var products = new List<Product>();

        // Una sola instancia de Store para todos los productos.
        var store = new Store
        {
            Code = "UNIMARC",
            Name = "Unimarc"
        };

        foreach (var source in availableProducts.EnumerateArray())
        {
            var rawJson = source.GetRawText();

            using var prettyDocument = JsonDocument.Parse(rawJson);

            var formattedJson = JsonSerializer.Serialize(prettyDocument.RootElement,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            Console.WriteLine("[UNIMARC] PRODUCTO ORIGINAL:");
            Console.WriteLine(formattedJson);
            
            
            if (source.ValueKind != JsonValueKind.Object)
                continue;

            if (!source.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object)
                continue;

            if (!source.TryGetProperty("price", out var priceData) || priceData.ValueKind != JsonValueKind.Object)
                continue;

            var name = GetString(item, "name");
            var slug = GetString(item, "slug");
            var brand = GetString(item, "brand");
            var priceText = GetString(priceData, "price");
            var measurementUnit = GetString(item, "measurementUnitUn");
            
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug))
                continue;

            if (!TryParsePrice(priceText, out var price) || price <= 0)
                continue;

            if (!Uri.TryCreate(BaseUri, slug, out var productUrl))
                continue;

            var imageUrl = GetImageUrl(item);
            var categories = GetCategories(item);
            var availability = GetAvailability(priceData);

            var product = new Product(
                name: name,
                price: price,
                store: store,
                productUrl: productUrl,
                imageUrl: imageUrl,
                brand: brand,
                sellerName: null,
                categories: categories,
                availability: availability
            );
            /*product.Clasification = new Clasification
            {
                Measurement = new Measurement(
                    UnitType: null,
                    Quantity: 1,
                    QuantityUnit: 1,
                    Unit: null
                )
            };*/
            

            products.Add(product);
        }

        return products
            .DistinctBy(product => product.ProductUrl)
            .ToArray();
    }

    private static string? GetString(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var property))
            return null;

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static IReadOnlyCollection<string> GetCategories(
        JsonElement item)
    {
        if (!item.TryGetProperty(
                "categories",
                out var categories)
            || categories.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return categories
            .EnumerateArray()
            .Where(value =>
                value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString())
            .OfType<string>()
            .Where(value =>
                !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Uri? GetImageUrl(JsonElement item)
    {
        if (!item.TryGetProperty(
                "images",
                out var images)
            || images.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var image in images.EnumerateArray())
        {
            if (image.ValueKind != JsonValueKind.String)
                continue;

            var imageText = image.GetString();

            if (Uri.TryCreate(
                    imageText,
                    UriKind.Absolute,
                    out var imageUrl)
                && imageUrl.Scheme == Uri.UriSchemeHttps)
            {
                return imageUrl;
            }
        }

        return null;
    }

    private static string? GetAvailability(
        JsonElement priceData)
    {
        if (!priceData.TryGetProperty(
                "availableQuantity",
                out var quantity))
        {
            return null;
        }

        if (quantity.ValueKind != JsonValueKind.Number
            || !quantity.TryGetInt32(
                out var availableQuantity))
        {
            return null;
        }

        return availableQuantity > 0
            ? "Disponible"
            : "Sin stock";
    }

    private static bool TryParsePrice(
        string? priceText,
        out decimal price)
    {
        price = 0;

        if (string.IsNullOrWhiteSpace(priceText))
            return false;

        // Ejemplo:
        // "$1.000" -> "1000"
        // "$12.990" -> "12990"

        var normalized = priceText
            .Replace("$", "")
            .Replace(".", "")
            .Replace(" ", "")
            .Trim();

        return decimal.TryParse(
            normalized,
            NumberStyles.Number,
            CultureInfo.GetCultureInfo("es-CL"),
            out price);
    }
}
