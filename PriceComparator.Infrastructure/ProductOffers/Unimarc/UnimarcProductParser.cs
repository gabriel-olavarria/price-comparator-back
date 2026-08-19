using System.Globalization;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Infrastructure.ProductOffers.Unimarc;

public sealed class UnimarcProductParser
{
    private static readonly Uri BaseUri = new("https://www.unimarc.cl");

    public async Task<IReadOnlyCollection<ProductOffer>> ParseAsync(string html, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(html, cancellationToken);
        var productCards = document.QuerySelectorAll("section[id^='shelf__vertical--']");
        Console.WriteLine($"[UNIMARC] Tarjetas encontradas: {productCards.Length}");
        var products = new List<ProductOffer>();
        foreach (var card in productCards)
        {
            var product = TryCreateProductOffer(card);

            if (product is not null)
            {
                products.Add(product);
            }
        }

        return products.GroupBy(product => product.ProductUrl) .Select(group => group.First()) .ToArray();
    }

    private static ProductOffer? TryCreateProductOffer(IElement card)
    {
        var nameElement = card.QuerySelector("p[class*='Shelf_nameProduct__']");
        var brandElement = card.QuerySelector("p[class*='Shelf_brandText__']");
        var productLinkElement = card.QuerySelector("a[href^='/product/']");
        var imageElement = card.QuerySelector("img");
        var priceElement = card.QuerySelector("p[id^='listPrice__offerPrice--listprice-']");
        var name = nameElement?.TextContent.Trim();
        
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var relativeUrl = productLinkElement?.GetAttribute("href");
        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            return null;
        }

        var productUrl = new Uri(BaseUri, relativeUrl);
        var priceText = priceElement?.TextContent;
        
        if (!TryParsePrice(priceText, out var price))
        {
            return null;
        }

        Uri? imageUrl = null;
        var imageSource = imageElement?.GetAttribute("src");
        if (!string.IsNullOrWhiteSpace(imageSource))
        {
            Uri.TryCreate(imageSource, UriKind.Absolute, out imageUrl);
        }
        var brand = brandElement?.TextContent.Trim();
        var store = new Store( code: "UNIMARC", name: "Unimarc");

        return new ProductOffer(
            name: name,
            price: price,
            store: store,
            productUrl: productUrl,
            imageUrl: imageUrl,
            brand: brand,
            sellerName: "Unimarc",
            sellerType: "INTERNAL",
            categories: [],
            availability: null);
    }

    private static bool TryParsePrice(string? priceText, out decimal price)
    {
        price = 0;

        if (string.IsNullOrWhiteSpace(priceText))
        {
            return false;
        }

        var normalizedPrice = priceText
            .Replace("$", string.Empty)
            .Replace(".", string.Empty)
            .Replace("c/u", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        return decimal.TryParse( normalizedPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out price);
    }
}