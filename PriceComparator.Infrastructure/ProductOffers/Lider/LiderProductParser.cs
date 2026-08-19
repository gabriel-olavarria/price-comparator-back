using System.Text.Json;
using AngleSharp.Html.Parser;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Infrastructure.ProductOffers.Lider;

public sealed class LiderProductParser
{
    private static readonly Uri LiderBaseUri =
        new("https://www.lider.cl");

    public async Task<IReadOnlyCollection<ProductOffer>> ParseAsync(
        string html,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var parser =
            new HtmlParser();

        var document =
            await parser.ParseDocumentAsync(
                html,
                cancellationToken);

        var nextDataElement =
            document.QuerySelector(
                "script#__NEXT_DATA__");

        if (nextDataElement is null ||
            string.IsNullOrWhiteSpace(
                nextDataElement.TextContent))
        {
            return [];
        }

        using var jsonDocument =
            JsonDocument.Parse(
                nextDataElement.TextContent);

        var offers =
            new List<ProductOffer>();

        FindProducts(
            jsonDocument.RootElement,
            offers);

        return offers
            .GroupBy(
                offer => offer.ProductUrl)
            .Select(
                group => group.First())
            .ToArray();
    }

    private void FindProducts(
        JsonElement element,
        ICollection<ProductOffer> offers)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                if (IsProduct(element))
                {
                    var offer =
                        TryCreateProductOffer(
                            element);

                    if (offer is not null)
                    {
                        offers.Add(offer);
                    }
                }

                foreach (
                    var property
                    in element.EnumerateObject())
                {
                    FindProducts(
                        property.Value,
                        offers);
                }

                break;
            }

            case JsonValueKind.Array:
            {
                foreach (
                    var item
                    in element.EnumerateArray())
                {
                    FindProducts(
                        item,
                        offers);
                }

                break;
            }
        }
    }

    private static bool IsProduct(
        JsonElement element)
    {
        return element.TryGetProperty(
                   "__typename",
                   out var typeProperty)
               &&
               typeProperty.ValueKind ==
               JsonValueKind.String
               &&
               typeProperty.GetString() ==
               "Product"
               &&
               element.TryGetProperty(
                   "name",
                   out _)
               &&
               element.TryGetProperty(
                   "price",
                   out _)
               &&
               element.TryGetProperty(
                   "canonicalUrl",
                   out _);
    }

    private static ProductOffer?
        TryCreateProductOffer(
            JsonElement element)
    {
        if (!TryGetString(
                element,
                "name",
                out var name))
        {
            return null;
        }

        if (!TryGetDecimal(
                element,
                "price",
                out var price) ||
            price <= 0)
        {
            return null;
        }

        if (!TryGetString(
                element,
                "canonicalUrl",
                out var relativeUrl))
        {
            return null;
        }

        var productUrl =
            new Uri(
                LiderBaseUri,
                relativeUrl);

        Uri? imageUrl = null;

        if (TryGetString(
                element,
                "image",
                out var image))
        {
            Uri.TryCreate(
                image,
                UriKind.Absolute,
                out imageUrl);
        }

        TryGetString(
            element,
            "brand",
            out var brand);

        TryGetString(
            element,
            "sellerName",
            out var sellerName);

        TryGetString(
            element,
            "sellerType",
            out var sellerType);

        var categories =
            GetCategories(element);

        var availability =
            GetAvailability(element);

        var store =
            new Store(
                code: "LIDER",
                name: "Lider");

        return new ProductOffer(
            name: name,
            price: price,
            store: store,
            productUrl: productUrl,
            imageUrl: imageUrl,
            brand: brand,
            sellerName: sellerName,
            sellerType: sellerType,
            categories: categories,
            availability: availability);
    }

    private static bool TryGetString(
        JsonElement element,
        string propertyName,
        out string value)
    {
        value = string.Empty;

        if (!element.TryGetProperty(
                propertyName,
                out var property))
        {
            return false;
        }

        if (property.ValueKind !=
            JsonValueKind.String)
        {
            return false;
        }

        var result =
            property.GetString();

        if (string.IsNullOrWhiteSpace(
                result))
        {
            return false;
        }

        value =
            result.Trim();

        return true;
    }

    private static bool TryGetDecimal(
        JsonElement element,
        string propertyName,
        out decimal value)
    {
        value = 0;

        if (!element.TryGetProperty(
                propertyName,
                out var property))
        {
            return false;
        }

        return property.ValueKind ==
               JsonValueKind.Number
               &&
               property.TryGetDecimal(
                   out value);
    }

    private static IReadOnlyCollection<string>
        GetCategories(
            JsonElement product)
    {
        if (!product.TryGetProperty(
                "category",
                out var category)
            ||
            category.ValueKind !=
            JsonValueKind.Object)
        {
            return [];
        }

        if (!category.TryGetProperty(
                "path",
                out var path)
            ||
            path.ValueKind !=
            JsonValueKind.Array)
        {
            return [];
        }

        var categories =
            new List<string>();

        foreach (
            var categoryItem
            in path.EnumerateArray())
        {
            if (TryGetString(
                    categoryItem,
                    "name",
                    out var categoryName))
            {
                categories.Add(
                    categoryName);
            }
        }

        return categories;
    }

    private static string? GetAvailability( JsonElement product)
    {
        if (!product.TryGetProperty("availabilityStatusV2", out var availability) || availability.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return TryGetString( availability, "value", out var value) ? value : null;
    }
}