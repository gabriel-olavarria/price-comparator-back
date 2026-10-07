using System.Text.Json;
using AngleSharp.Html.Parser;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Infrastructure.ProductOffers.SantaIsabel;

public sealed class SantaIsabelProductParser
{
    private static readonly Uri SantaIsabelBaseUri = new("https://www.santaisabel.cl");

    public async Task<IReadOnlyCollection<ProductOffer>> ParseAsync(string html, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(html, cancellationToken);

        /*
         * Santa Isabel incluye los datos completos de la PLP
         * dentro de:
         *
         * window.__renderData = "...";
         */
        var renderDataScript = document.Scripts.FirstOrDefault(script => script.TextContent.Contains("window.__renderData", StringComparison.Ordinal));

        if (renderDataScript is null)
        {
            Console.WriteLine("[SANTA ISABEL] No se encontró window.__renderData.");

            return [];
        }

        var renderDataJson = ExtractRenderDataJson(renderDataScript.TextContent);
        if (string.IsNullOrWhiteSpace(renderDataJson))
        {
            Console.WriteLine("[SANTA ISABEL] No se pudo extraer window.__renderData.");
            return [];
        }

        try
        {
            using var jsonDocument = JsonDocument.Parse(renderDataJson);
            var offers = ParseProducts(jsonDocument.RootElement);
            Console.WriteLine($"[SANTA ISABEL] Productos extraídos: {offers.Count}");
            return offers;
        }
        catch (JsonException exception)
        {
            Console.WriteLine("[SANTA ISABEL] Error leyendo __renderData: " + exception.Message);
            return [];
        }
    }

    private static string? ExtractRenderDataJson(string script)
    {
        const string marker = "window.__renderData";

        var markerIndex = script.IndexOf(marker, StringComparison.Ordinal);

        if (markerIndex < 0)
        {
            return null;
        }

        var equalsIndex = script.IndexOf('=', markerIndex + marker.Length);

        if (equalsIndex < 0)
        {
            return null;
        }

        var value = script[(equalsIndex + 1)..].Trim();

        /*
         * El script termina normalmente en:
         *
         * window.__renderData = "...";
         */
        if (value.EndsWith(';'))
        {
            value =
                value[..^1].Trim();
        }

        /*
         * __renderData no contiene directamente un objeto JSON.
         *
         * Es un STRING JavaScript que internamente contiene JSON:
         *
         * "{\"plp\":{...}}"
         *
         * JsonSerializer.Deserialize<string>() elimina correctamente
         * las comillas externas y los caracteres escapados.
         */
        try
        {
            return JsonSerializer.Deserialize<string>(
                value);
        }
        catch (JsonException exception)
        {
            Console.WriteLine(
                "[SANTA ISABEL] No se pudo deserializar " +
                "window.__renderData: " +
                exception.Message);

            return null;
        }
    }

    private static IReadOnlyCollection<ProductOffer> ParseProducts(JsonElement root)
    {
        /*
         * Estructura:
         *
         * plp
         *   -> plp_products
         *       -> products[]
         */
        if (!root.TryGetProperty(
                "plp",
                out var plp) ||
            plp.ValueKind !=
            JsonValueKind.Object)
        {
            return [];
        }

        if (!plp.TryGetProperty(
                "plp_products",
                out var plpProducts) ||
            plpProducts.ValueKind !=
            JsonValueKind.Object)
        {
            return [];
        }

        if (!plpProducts.TryGetProperty(
                "products",
                out var products) ||
            products.ValueKind !=
            JsonValueKind.Array)
        {
            return [];
        }

        var offers =
            new List<ProductOffer>();

        foreach (
            var product
            in products.EnumerateArray())
        {
            var offer =
                TryCreateProductOffer(
                    product);

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
            JsonElement product)
    {
        if (!TryGetString(
                product,
                "productName",
                out var name))
        {
            return null;
        }

        TryGetString(
            product,
            "brand",
            out var brand);

        /*
         * Santa Isabel entrega linkText.
         *
         * Ejemplo:
         *
         * arroz-largo-fino-1-kg-cuisine-and-co-1845786
         */
        if (!TryGetString(
                product,
                "linkText",
                out var linkText))
        {
            return null;
        }

        var productUrl =
            new Uri(
                SantaIsabelBaseUri,
                $"/{linkText}/p");

        var categories =
            GetCategories(product);

        /*
         * Un producto puede tener uno o más SKUs.
         *
         * Para nuestro comparador utilizamos el primer SKU
         * que tenga seller y precio válido.
         */
        if (!product.TryGetProperty(
                "items",
                out var items) ||
            items.ValueKind !=
            JsonValueKind.Array)
        {
            return null;
        }

        foreach (
            var item
            in items.EnumerateArray())
        {
            var offer =
                TryCreateProductOfferFromItem(
                    item,
                    name,
                    brand,
                    productUrl,
                    categories);

            if (offer is not null)
            {
                return offer;
            }
        }

        return null;
    }

    private static ProductOffer? TryCreateProductOfferFromItem(JsonElement item, string name, string? brand, Uri productUrl, IReadOnlyCollection<string> categories)
    {
        Uri? imageUrl = GetImageUrl(item);
        if (!item.TryGetProperty("sellers", out var sellers) || sellers.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var seller in sellers.EnumerateArray())
        {
            if (!seller.TryGetProperty("commertialOffer", out var commercialOffer) || commercialOffer.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            /*
             * VTEX lo escribe como "commertialOffer".
             * Aunque parezca un typo, debemos respetar el nombre
             * que realmente viene en el JSON.
             */
            if (!TryGetDecimal(commercialOffer, "Price", out var price) || price <= 0)
            {
                continue;
            }

            TryGetString(seller, "sellerName", out var sellerName);
            var availability = GetAvailability(commercialOffer);

            return new ProductOffer(
                name: name,
                price: price,
                store: new Store(code: "SANTA_ISABEL", name: "Santa Isabel"),
                productUrl: productUrl,
                imageUrl: imageUrl,
                brand: brand,
                sellerName: string.IsNullOrWhiteSpace(sellerName) ? "Santa Isabel" : sellerName,
                categories: categories,
                availability: availability);
        }

        return null;
    }

    private static IReadOnlyCollection<string> GetCategories(JsonElement product)
    {
        if (!product.TryGetProperty("categories", out var categoriesElement) || categoriesElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var categoryElement in categoriesElement.EnumerateArray())
        {
            if (categoryElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var categoryPath = categoryElement.GetString();

            if (string.IsNullOrWhiteSpace(
                    categoryPath))
            {
                continue;
            }

            foreach (var category in categoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                categories.Add(category);
            }
        }

        return categories.ToArray();
    }

    private static Uri? GetImageUrl(JsonElement item)
    {
        if (!item.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var image in images.EnumerateArray())
        {
            if (!TryGetString(image, "imageUrl", out var imageUrlText))
            {
                continue;
            }

            if (Uri.TryCreate(imageUrlText, UriKind.Absolute, out var imageUrl))
            {
                return imageUrl;
            }
        }

        return null;
    }

    private static string?
        GetAvailability(
            JsonElement commercialOffer)
    {
        /*
         * Santa Isabel no entrega schema.org availability
         * en __renderData.
         *
         * Sí entrega AvailableQuantity.
         */
        if (!commercialOffer.TryGetProperty(
                "AvailableQuantity",
                out var quantityElement) ||
            quantityElement.ValueKind !=
            JsonValueKind.Number ||
            !quantityElement.TryGetInt32(
                out var quantity))
        {
            return null;
        }

        return quantity > 0
            ? "IN_STOCK"
            : "OUT_OF_STOCK";
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

    private static bool TryGetDecimal(JsonElement element, string propertyName, out decimal value)
    {
        value = 0;

        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        return property.TryGetDecimal(out value);
    }
}