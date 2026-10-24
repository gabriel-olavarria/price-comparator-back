using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Infrastructure.ProductOffers.Tottus;

public sealed class TottusProductParser : ISingletonDependency
{
    public async Task<IReadOnlyCollection<Product>> ParseAsync(string html,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(html, cancellationToken);
        var jsonLdElement = FindProductItemList(document);

        if (jsonLdElement is null || string.IsNullOrWhiteSpace(jsonLdElement.TextContent))
        {
            Console.WriteLine("No se encontró el JSON-LD ItemList de Tottus.");
            return [];
        }

        var json = WebUtility.HtmlDecode(jsonLdElement.TextContent);
        using var jsonDocument = JsonDocument.Parse(json);
        var offers = ParseProducts(jsonDocument.RootElement);
        Console.WriteLine($"Productos extraídos desde Tottus: {offers.Count}");
        return offers;
    }

    private static IElement? FindProductItemList(IDocument document)
    {
        var jsonLdElements = document.QuerySelectorAll("script[type='application/ld+json']");

        foreach (var element in jsonLdElements)
        {
            if (string.IsNullOrWhiteSpace(element.TextContent))
            {
                continue;
            }

            try
            {
                var json = WebUtility.HtmlDecode(element.TextContent);
                using var jsonDocument = JsonDocument.Parse(json);
                var root = jsonDocument.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (TryGetString(root, "@type", out var type) &&
                    string.Equals(type, "ItemList", StringComparison.OrdinalIgnoreCase))
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

    private static IReadOnlyCollection<Product> ParseProducts(JsonElement root)
    {
        if (!root.TryGetProperty("itemListElement", out var itemListElement) ||
            itemListElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var offers = new List<Product>();

        foreach (var listItem in itemListElement.EnumerateArray())
        {
            var offer = TryCreateProductOffer(listItem);

            if (offer is not null)
            {
                offers.Add(offer);
            }
        }

        return offers
            .GroupBy(offer => offer.ProductUrl)
            .Select(group => group.First())
            .ToArray();
    }

    private static Product? TryCreateProductOffer(JsonElement listItem)
    {
        if (!listItem.TryGetProperty("item", out var product) || product.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!TryGetString(product, "name", out var name))
        {
            return null;
        }

        if (!TryGetString(product, "url", out var productUrlText) ||
            !Uri.TryCreate(productUrlText, UriKind.Absolute, out var productUrl))
        {
            return null;
        }

        Uri? imageUrl = null;

        if (TryGetString(product, "image", out var imageUrlText))
        {
            Uri.TryCreate(imageUrlText, UriKind.Absolute, out imageUrl);
        }

        string? brand = null;

        if (product.TryGetProperty("brand", out var brandElement) && brandElement.ValueKind == JsonValueKind.Object &&
            TryGetString(brandElement, "name", out var brandName))
        {
            brand = brandName;
        }

        if (!TryGetPrice(product, out var price))
        {
            return null;
        }

        var store = new Store { Code = "TOTTUS", Name = "Tottus" };
        return new Product(
            name: name,
            price: price,
            store: store,
            productUrl: productUrl,
            imageUrl: imageUrl,
            brand: brand,
            sellerName: "Tottus",
            categoriesStore: [],
            availability: null);
    }

    private static bool TryGetPrice(JsonElement product, out decimal price)
    {
        price = 0;
        if (!product.TryGetProperty("offers", out var offers) || offers.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var offer in offers.EnumerateArray())
        {
            if (!TryGetString(offer, "price", out var priceText))
            {
                continue;
            }

            if (decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out price) && price > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var result = property.GetString();

        if (string.IsNullOrWhiteSpace(result))
        {
            return false;
        }

        value = result.Trim();
        return true;
    }


    public async Task<string> ExtractRawJsonAsync(string html, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "[]";

        var htmlParser = new HtmlParser();
        var document = await htmlParser.ParseDocumentAsync(html, cancellationToken);
        var nextDataScript = document.QuerySelector("script#__NEXT_DATA__");

        if (nextDataScript is null || string.IsNullOrWhiteSpace(nextDataScript.TextContent))
        {
            Console.WriteLine("[TOTTUS][RAW] No se encontró __NEXT_DATA__.");

            return "[]";
        }

        using var jsonDocument = JsonDocument.Parse(nextDataScript.TextContent);
        var root = jsonDocument.RootElement;

        if (!root.TryGetProperty("props", out var props) || !props.TryGetProperty("pageProps", out var pageProps) ||
            !pageProps.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            Console.WriteLine("[TOTTUS][RAW] No se encontró pageProps.results.");
            return "[]";
        }

        var categoryNames = GetCategoryNames(document);
        var products = new JsonArray();

        foreach (var product in results.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (product.ValueKind != JsonValueKind.Object)
                continue;

            // Conservamos el producto original completo.
            var productNode = JsonNode.Parse(product.GetRawText())!.AsObject();

            // Incorporamos la jerarquía de categorías
            // obtenida desde el HTML.
            if (categoryNames.Count > 0)
            {
                productNode["resolvedCategoryNames"] =
                    new JsonArray(categoryNames
                        .Select(category => (JsonNode?)JsonValue.Create(category))
                        .ToArray());
            }

            products.Add(productNode);
        }

        Console.WriteLine(
            $"[TOTTUS][RAW] Productos extraídos: {products.Count}");

        Console.WriteLine(
            $"[TOTTUS][RAW] Categorías: {string.Join(" > ", categoryNames)}");

        return products.ToJsonString(
            new JsonSerializerOptions
            {
                WriteIndented = true
            });
    }

    private static IReadOnlyCollection<string> GetCategoryNames(AngleSharp.Dom.IDocument document)
    {
        var scripts = document.QuerySelectorAll("script[type='application/ld+json']");

        foreach (var script in scripts)
        {
            if (string.IsNullOrWhiteSpace(script.TextContent))
                continue;

            try
            {
                using var jsonDocument = JsonDocument.Parse(
                    script.TextContent);

                var root = jsonDocument.RootElement;

                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("@type", out var type) ||
                    type.GetString() != "BreadcrumbList")
                {
                    continue;
                }

                if (!root.TryGetProperty(
                        "itemListElement",
                        out var items) ||
                    items.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var categories = new List<string>();

                foreach (var item in items.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object ||
                        !item.TryGetProperty("name", out var name) ||
                        name.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var category = name.GetString();

                    if (!string.IsNullOrWhiteSpace(category))
                        categories.Add(category.Trim());
                }

                return categories
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (JsonException)
            {
                // Ignoramos otros scripts JSON-LD inválidos.
            }
        }

        return [];
    }
}