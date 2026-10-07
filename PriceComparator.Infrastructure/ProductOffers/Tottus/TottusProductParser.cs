using System.Globalization;
using System.Net;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Infrastructure.ProductOffers.Tottus;

public sealed class TottusProductParser
{
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

        var jsonLdElement = FindProductItemList(document);

        if (jsonLdElement is null ||
            string.IsNullOrWhiteSpace(jsonLdElement.TextContent))
        {
            Console.WriteLine(
                "No se encontró el JSON-LD ItemList de Tottus.");

            return [];
        }

        var json =
            WebUtility.HtmlDecode(
                jsonLdElement.TextContent);

        using var jsonDocument =
            JsonDocument.Parse(json);

        var offers =
            ParseProducts(
                jsonDocument.RootElement);

        Console.WriteLine(
            $"Productos extraídos desde Tottus: {offers.Count}");

        return offers;
    }

    private static IElement? FindProductItemList(
        IDocument document)
    {
        var jsonLdElements =
            document.QuerySelectorAll(
                "script[type='application/ld+json']");

        foreach (var element in jsonLdElements)
        {
            if (string.IsNullOrWhiteSpace(
                    element.TextContent))
            {
                continue;
            }

            try
            {
                var json =
                    WebUtility.HtmlDecode(
                        element.TextContent);

                using var jsonDocument =
                    JsonDocument.Parse(json);

                var root =
                    jsonDocument.RootElement;

                if (root.ValueKind !=
                    JsonValueKind.Object)
                {
                    continue;
                }

                if (TryGetString(
                        root,
                        "@type",
                        out var type) &&
                    string.Equals(
                        type,
                        "ItemList",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return element;
                }
            }
            catch (JsonException)
            {
                // Ignoramos otros JSON-LD.
            }
        }

        return null;
    }

    private static IReadOnlyCollection<ProductOffer>
        ParseProducts(
            JsonElement root)
    {
        if (!root.TryGetProperty(
                "itemListElement",
                out var itemListElement) ||
            itemListElement.ValueKind !=
            JsonValueKind.Array)
        {
            return [];
        }

        var offers =
            new List<ProductOffer>();

        foreach (var listItem
                 in itemListElement.EnumerateArray())
        {
            var offer =
                TryCreateProductOffer(listItem);

            if (offer is not null)
            {
                offers.Add(offer);
            }
        }

        return offers
            .GroupBy(
                offer => offer.ProductUrl)
            .Select(
                group => group.First())
            .ToArray();
    }

    private static ProductOffer? TryCreateProductOffer(JsonElement listItem)
    {
        if (!listItem.TryGetProperty("item", out var product) || product.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!TryGetString(product, "name", out var name))
        {
            return null;
        }

        if (!TryGetString(product, "url", out var productUrlText) || !Uri.TryCreate(productUrlText, UriKind.Absolute, out var productUrl))
        {
            return null;
        }

        Uri? imageUrl = null;

        if (TryGetString(product, "image", out var imageUrlText))
        {
            Uri.TryCreate(imageUrlText, UriKind.Absolute, out imageUrl);
        }

        string? brand = null;

        if (product.TryGetProperty("brand", out var brandElement) && brandElement.ValueKind == JsonValueKind.Object && TryGetString(brandElement, "name", out var brandName))
        {
            brand = brandName;
        }

        if (!TryGetPrice(product, out var price))
        {
            return null;
        }

        return new ProductOffer(
            name: name,
            price: price,
            store: new Store(
                code: "TOTTUS",
                name: "Tottus"),
            productUrl: productUrl,
            imageUrl: imageUrl,
            brand: brand,
            sellerName: "Tottus",
            categories: [],
            availability: null);
    }

    private static bool TryGetPrice(
        JsonElement product,
        out decimal price)
    {
        price = 0;

        if (!product.TryGetProperty(
                "offers",
                out var offers) ||
            offers.ValueKind !=
            JsonValueKind.Array)
        {
            return false;
        }

        foreach (var offer in offers.EnumerateArray())
        {
            if (!TryGetString(
                    offer,
                    "price",
                    out var priceText))
            {
                continue;
            }

            if (decimal.TryParse(
                    priceText,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out price) &&
                price > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetString(
        JsonElement element,
        string propertyName,
        out string value)
    {
        value = string.Empty;

        if (!element.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind !=
            JsonValueKind.String)
        {
            return false;
        }

        var result =
            property.GetString();

        if (string.IsNullOrWhiteSpace(result))
        {
            return false;
        }

        value = result.Trim();

        return true;
    }
}