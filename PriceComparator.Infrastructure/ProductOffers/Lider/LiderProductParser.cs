using System.Globalization;
using System.Text.Json;
using AngleSharp.Html.Parser;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Infrastructure.ProductOffers.Lider;

public sealed class LiderProductParser
{
    private static readonly Uri LiderBaseUri =
        new("https://super.lider.cl");

    public async Task<IReadOnlyCollection<ProductOffer>> ParseAsync(
        string html,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var parser = new HtmlParser();

        var document = await parser.ParseDocumentAsync(
            html,
            cancellationToken);

        var nextDataElement =
            document.QuerySelector(
                "script#__NEXT_DATA__");

        if (nextDataElement is null ||
            string.IsNullOrWhiteSpace(
                nextDataElement.TextContent))
        {
            Console.WriteLine(
                "[LIDER][PARSER] No se encontró __NEXT_DATA__.");

            return [];
        }

        try
        {
            using var jsonDocument =
                JsonDocument.Parse(
                    nextDataElement.TextContent);

            if (!TryFindSearchResult(
                    jsonDocument.RootElement,
                    out var searchResult))
            {
                Console.WriteLine(
                    "[LIDER][PARSER] No se encontró searchResult.");

                return [];
            }

            var offers =
                ParseOrganicProducts(
                    searchResult);

            Console.WriteLine(
                $"[LIDER][PARSER] Productos orgánicos extraídos: {offers.Count}");

            return offers;
        }
        catch (JsonException exception)
        {
            Console.WriteLine(
                $"[LIDER][PARSER] JSON inválido: {exception.Message}");

            return [];
        }
    }

    private static bool TryFindSearchResult(
        JsonElement element,
        out JsonElement searchResult)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(
                    "searchResult",
                    out var result) &&
                result.ValueKind == JsonValueKind.Object)
            {
                searchResult = result;
                return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (TryFindSearchResult(
                        property.Value,
                        out searchResult))
                {
                    return true;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryFindSearchResult(
                        item,
                        out searchResult))
                {
                    return true;
                }
            }
        }

        searchResult = default;

        return false;
    }

    private static IReadOnlyCollection<ProductOffer>
        ParseOrganicProducts(
            JsonElement searchResult)
    {
        if (!searchResult.TryGetProperty(
                "itemStacks",
                out var itemStacks) ||
            itemStacks.ValueKind !=
            JsonValueKind.Array)
        {
            return [];
        }

        var offers =
            new List<ProductOffer>();

        foreach (var stack in itemStacks.EnumerateArray())
        {
            if (stack.ValueKind !=
                JsonValueKind.Object)
            {
                continue;
            }

            if (IsSponsoredStack(stack))
            {
                continue;
            }

            if (!stack.TryGetProperty(
                    "items",
                    out var items) ||
                items.ValueKind !=
                JsonValueKind.Array)
            {
                continue;
            }

            foreach (var product in items.EnumerateArray())
            {
                if (product.ValueKind !=
                    JsonValueKind.Object)
                {
                    continue;
                }

                if (!IsProduct(product))
                {
                    continue;
                }

                var offer =
                    TryCreateProductOffer(
                        product);

                if (offer is not null)
                {
                    offers.Add(offer);
                }
            }
        }

        return offers
            .GroupBy(
                offer => offer.ProductUrl.AbsoluteUri,
                StringComparer.OrdinalIgnoreCase)
            .Select(
                group => group.First())
            .ToArray();
    }

    private static bool IsSponsoredStack(
        JsonElement stack)
    {
        if (!stack.TryGetProperty(
                "meta",
                out var meta) ||
            meta.ValueKind !=
            JsonValueKind.Object)
        {
            return false;
        }

        if (!meta.TryGetProperty(
                "isSponsored",
                out var sponsored))
        {
            return false;
        }

        return sponsored.ValueKind ==
               JsonValueKind.True;
    }

    private static bool IsProduct(
        JsonElement product)
    {
        if (!product.TryGetProperty(
                "__typename",
                out var typeProperty))
        {
            return false;
        }

        return typeProperty.ValueKind ==
               JsonValueKind.String
               &&
               string.Equals(
                   typeProperty.GetString(),
                   "Product",
                   StringComparison.Ordinal);
    }

    private static ProductOffer?
        TryCreateProductOffer(
            JsonElement product)
    {
        if (!TryGetString(
                product,
                "name",
                out var name))
        {
            return null;
        }

        if (!TryGetDecimal(
                product,
                "price",
                out var price) ||
            price <= 0)
        {
            price = TryGetPriceFromPriceInfo(
                product);

            if (price <= 0)
            {
                return null;
            }
        }

        if (!TryGetString(
                product,
                "canonicalUrl",
                out var productUrlText))
        {
            return null;
        }

        if (!TryCreateAbsoluteUri(
                productUrlText,
                out var productUrl))
        {
            return null;
        }

        var imageUrl =
            GetImageUrl(
                product);

        TryGetString(
            product,
            "brand",
            out var brand);

        TryGetString(
            product,
            "sellerName",
            out var sellerName);

        TryGetString(
            product,
            "sellerType",
            out var sellerType);

        var categories =
            GetCategories(
                product);

        var availability =
            GetAvailability(
                product);

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

    private static decimal TryGetPriceFromPriceInfo(
        JsonElement product)
    {
        if (!product.TryGetProperty(
                "priceInfo",
                out var priceInfo) ||
            priceInfo.ValueKind !=
            JsonValueKind.Object)
        {
            return 0;
        }

        if (!priceInfo.TryGetProperty(
                "currentPrice",
                out var currentPrice) ||
            currentPrice.ValueKind !=
            JsonValueKind.Object)
        {
            return 0;
        }

        return TryGetDecimal(
            currentPrice,
            "price",
            out var price)
            ? price
            : 0;
    }

    private static Uri? GetImageUrl(
        JsonElement product)
    {
        if (TryGetString(
                product,
                "image",
                out var directImage) &&
            TryCreateAbsoluteUri(
                directImage,
                out var directImageUrl))
        {
            return directImageUrl;
        }

        if (product.TryGetProperty(
                "imageInfo",
                out var imageInfo) &&
            imageInfo.ValueKind ==
            JsonValueKind.Object &&
            TryGetString(
                imageInfo,
                "thumbnailUrl",
                out var thumbnailUrl) &&
            TryCreateAbsoluteUri(
                thumbnailUrl,
                out var imageInfoUrl))
        {
            return imageInfoUrl;
        }

        return null;
    }

    private static IReadOnlyCollection<string>
        GetCategories(
            JsonElement product)
    {
        if (!product.TryGetProperty(
                "category",
                out var category) ||
            category.ValueKind !=
            JsonValueKind.Object)
        {
            return [];
        }

        var categories =
            new List<string>();

        if (category.TryGetProperty(
                "path",
                out var path) &&
            path.ValueKind ==
            JsonValueKind.Array)
        {
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
        }

        if (TryGetString(
                category,
                "categoryPath",
                out var categoryPath))
        {
            categories.AddRange(
                categoryPath
                    .Split(
                        '/',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries));
        }

        return categories
            .Where(categoryName =>
                !string.IsNullOrWhiteSpace(
                    categoryName))
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? GetAvailability(
        JsonElement product)
    {
        if (TryGetString(
                product,
                "availabilityStatus",
                out var availabilityStatus))
        {
            return availabilityStatus;
        }

        if (TryGetString(
                product,
                "availabilityStatusDisplayValue",
                out var displayValue))
        {
            return NormalizeAvailability(
                displayValue);
        }

        if (product.TryGetProperty(
                "availabilityStatusV2",
                out var availabilityV2) &&
            availabilityV2.ValueKind ==
            JsonValueKind.Object &&
            TryGetString(
                availabilityV2,
                "value",
                out var availabilityValue))
        {
            return availabilityValue;
        }

        return null;
    }

    private static string NormalizeAvailability(
        string value)
    {
        if (string.Equals(
                value,
                "In stock",
                StringComparison.OrdinalIgnoreCase))
        {
            return "IN_STOCK";
        }

        if (string.Equals(
                value,
                "Out of stock",
                StringComparison.OrdinalIgnoreCase))
        {
            return "OUT_OF_STOCK";
        }

        return value;
    }

    private static bool TryCreateAbsoluteUri(
        string value,
        out Uri uri)
    {
        if (Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var absoluteUri))
        {
            uri = absoluteUri;

            return true;
        }

        if (Uri.TryCreate(
                LiderBaseUri,
                value,
                out var relativeUri))
        {
            uri = relativeUri;

            return true;
        }

        uri = null!;

        return false;
    }

    private static bool TryGetString(
        JsonElement element,
        string propertyName,
        out string value)
    {
        value =
            string.Empty;

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

        if (property.ValueKind ==
            JsonValueKind.Number)
        {
            return property.TryGetDecimal(
                out value);
        }

        if (property.ValueKind ==
            JsonValueKind.String)
        {
            return decimal.TryParse(
                property.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out value);
        }

        return false;
    }
}