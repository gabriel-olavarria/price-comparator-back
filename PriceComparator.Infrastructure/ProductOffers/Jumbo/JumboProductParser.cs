using System.Net;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Infrastructure.ProductOffers.Jumbo;

public sealed class JumboProductParser
{
    private static readonly Uri JumboBaseUri =
        new("https://www.jumbo.cl");

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

        /*
         * Las categorías vienen en los atributos
         * data-gtm-product-click de las tarjetas.
         */
        var categoriesByProductUrl =
            GetCategoriesByProductUrl(document);

        Console.WriteLine(
            $"Productos con categorías en Jumbo: " +
            $"{categoriesByProductUrl.Count}");

        /*
         * Los datos principales vienen en un JSON-LD
         * cuyo @type es ItemList.
         */
        var jsonLdElement =
            FindProductItemList(document);

        if (jsonLdElement is null ||
            string.IsNullOrWhiteSpace(
                jsonLdElement.TextContent))
        {
            Console.WriteLine(
                "No se encontró el JSON-LD ItemList de Jumbo.");

            return [];
        }

        using var jsonDocument =
            JsonDocument.Parse(
                jsonLdElement.TextContent);

        var offers =
            ParseProducts(
                jsonDocument.RootElement,
                categoriesByProductUrl);

        Console.WriteLine(
            $"Productos extraídos desde Jumbo: {offers.Count}");

        return offers;
    }

    private static IReadOnlyDictionary<
        string,
        IReadOnlyCollection<string>>
        GetCategoriesByProductUrl(
            IDocument document)
    {
        var result =
            new Dictionary<
                string,
                IReadOnlyCollection<string>>(
                StringComparer.OrdinalIgnoreCase);

        var productLinks =
            document.QuerySelectorAll(
                "a[data-gtm-product-click][href]");

        foreach (var productLink in productLinks)
        {
            var productUrl =
                productLink.GetAttribute("href");

            var encodedAnalyticsJson =
                productLink.GetAttribute(
                    "data-gtm-product-click");

            if (string.IsNullOrWhiteSpace(productUrl) ||
                string.IsNullOrWhiteSpace(
                    encodedAnalyticsJson))
            {
                continue;
            }

            /*
             * Convierte &quot; en comillas normales.
             *
             * AngleSharp normalmente ya decodifica atributos,
             * pero HtmlDecode hace que el código sea tolerante
             * si todavía quedan entidades HTML.
             */
            var analyticsJson =
                WebUtility.HtmlDecode(
                    encodedAnalyticsJson);

            try
            {
                using var analyticsDocument =
                    JsonDocument.Parse(
                        analyticsJson);

                var root =
                    analyticsDocument.RootElement;

                if (!TryGetString(
                        root,
                        "category",
                        out var categoryPath))
                {
                    continue;
                }

                var categories =
                    categoryPath
                        .Split(
                            '/',
                            StringSplitOptions.RemoveEmptyEntries |
                            StringSplitOptions.TrimEntries)
                        .Where(category =>
                            !string.IsNullOrWhiteSpace(
                                category))
                        .Distinct(
                            StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                if (categories.Length == 0)
                {
                    continue;
                }

                var normalizedPath =
                    NormalizeProductPath(
                        productUrl);

                result[normalizedPath] =
                    categories;
            }
            catch (JsonException exception)
            {
                Console.WriteLine(
                    "No se pudo leer " +
                    "data-gtm-product-click: " +
                    exception.Message);
            }
        }

        return result;
    }

    private static IElement?
        FindProductItemList(
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
                using var jsonDocument =
                    JsonDocument.Parse(
                        element.TextContent);

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
                /*
                 * Puede haber otros scripts JSON-LD
                 * no válidos o con estructuras diferentes.
                 */
            }
        }

        return null;
    }

    private static IReadOnlyCollection<ProductOffer>
        ParseProducts(
            JsonElement root,
            IReadOnlyDictionary<
                string,
                IReadOnlyCollection<string>>
                categoriesByProductUrl)
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

        foreach (
            var listItem
            in itemListElement.EnumerateArray())
        {
            var offer =
                TryCreateProductOffer(
                    listItem,
                    categoriesByProductUrl);

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

    private static ProductOffer?
        TryCreateProductOffer(
            JsonElement listItem,
            IReadOnlyDictionary<
                string,
                IReadOnlyCollection<string>>
                categoriesByProductUrl)
    {
        if (!listItem.TryGetProperty(
                "item",
                out var product) ||
            product.ValueKind !=
            JsonValueKind.Object)
        {
            return null;
        }

        if (!TryGetString(
                product,
                "name",
                out var name))
        {
            return null;
        }

        if (!TryGetString(
                product,
                "url",
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

        Uri? imageUrl = null;

        if (TryGetString(
                product,
                "image",
                out var imageUrlText))
        {
            Uri.TryCreate(
                imageUrlText,
                UriKind.Absolute,
                out imageUrl);
        }

        string? brand = null;

        if (product.TryGetProperty(
                "brand",
                out var brandElement) &&
            brandElement.ValueKind ==
            JsonValueKind.Object &&
            TryGetString(
                brandElement,
                "name",
                out var brandName))
        {
            brand = brandName;
        }

        if (!product.TryGetProperty(
                "offers",
                out var offerElement) ||
            offerElement.ValueKind !=
            JsonValueKind.Object)
        {
            return null;
        }

        if (!TryGetDecimal(
                offerElement,
                "price",
                out var price) ||
            price <= 0)
        {
            return null;
        }

        var availability =
            GetAvailability(
                offerElement);

        /*
         * El JSON-LD entrega una URL absoluta:
         *
         * https://www.jumbo.cl/producto/p
         *
         * El atributo href normalmente entrega:
         *
         * /producto/p
         *
         * Normalizamos ambos para poder relacionarlos.
         */
        var normalizedProductPath =
            NormalizeProductPath(
                productUrl.AbsolutePath);

        IReadOnlyCollection<string>
            categories = [];

        if (categoriesByProductUrl.TryGetValue(
                normalizedProductPath,
                out var productCategories))
        {
            categories =
                productCategories;
        }

        return new ProductOffer(
            name: name,
            price: price,
            store: new Store(
                code: "JUMBO",
                name: "Jumbo"),
            productUrl: productUrl,
            imageUrl: imageUrl,
            brand: brand,
            sellerName: "Jumbo",
            sellerType: "INTERNAL",
            categories: categories,
            availability: availability);
    }

    private static bool TryCreateAbsoluteUri(
        string value,
        out Uri productUrl)
    {
        if (Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var absoluteUri))
        {
            productUrl = absoluteUri;

            return true;
        }

        if (Uri.TryCreate(
                JumboBaseUri,
                value,
                out var relativeUri))
        {
            productUrl = relativeUri;

            return true;
        }

        productUrl = null!;

        return false;
    }

    private static string NormalizeProductPath(
        string url)
    {
        if (Uri.TryCreate(
                url,
                UriKind.Absolute,
                out var absoluteUri))
        {
            return absoluteUri
                .AbsolutePath
                .TrimEnd('/');
        }

        var pathWithoutQuery =
            url.Split(
                '?',
                StringSplitOptions.RemoveEmptyEntries)[0];

        if (!pathWithoutQuery.StartsWith('/'))
        {
            pathWithoutQuery =
                $"/{pathWithoutQuery}";
        }

        return pathWithoutQuery
            .TrimEnd('/');
    }

    private static string?
        GetAvailability(
            JsonElement offerElement)
    {
        if (!TryGetString(
                offerElement,
                "availability",
                out var availability))
        {
            return null;
        }

        return availability switch
        {
            "https://schema.org/InStock" =>
                "IN_STOCK",

            "https://schema.org/OutOfStock" =>
                "OUT_OF_STOCK",

            _ =>
                availability
        };
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
                out var property) ||
            property.ValueKind !=
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
                out var property) ||
            property.ValueKind !=
            JsonValueKind.Number)
        {
            return false;
        }

        return property.TryGetDecimal(
            out value);
    }
}