using PriceComparator.Application.Interfaces;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Browsers;
using PriceComparator.Infrastructure.Storage;

namespace PriceComparator.Infrastructure.ProductOffers.Unimarc;

public sealed class UnimarcProductOfferSearcher : IProductOfferSearcher , ITransientDependency
{
    private readonly PlaywrightHtmlBrowser _browser;
    private readonly UnimarcProductParser _parser;
    private readonly JsonProductStore _productStore;
    public string StoreCode => "Unimarc";

    public UnimarcProductOfferSearcher(PlaywrightHtmlBrowser browser, UnimarcProductParser parser, JsonProductStore productStore)
    {
        _browser = browser;
        _parser = parser;
        _productStore = productStore;
    }

    public async Task<IReadOnlyCollection<Product>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }
        var normalizedQuery = query.Trim();
        var storedOffers = await _productStore.GetAsync(StoreCode, normalizedQuery, cancellationToken);

        if (storedOffers is not null)
        {
            Console.WriteLine($"[UNIMARC] Usando productos almacenados: {storedOffers.Count}");
            return storedOffers;
        }

        Console.WriteLine("[UNIMARC] No hay datos almacenados vigentes. Consultando Unimarc.");

        var html = await SearchLiveAsync(normalizedQuery, cancellationToken);
        var offers = await _parser.ParseAsync(html, cancellationToken);
        await _productStore.SaveAsync(StoreCode, normalizedQuery, offers, cancellationToken);

        Console.WriteLine($"[UNIMARC] {offers.Count} productos guardados en JSON.");
        Console.WriteLine($"[UNIMARC] Productos encontrados: {offers.Count}");

        return offers;
    }

    private async Task<string> SearchLiveAsync(string query, CancellationToken cancellationToken)
    {
        var normalizedQuery = query.Trim().Replace(" ", "-");
        var encodedQuery = Uri.EscapeDataString(normalizedQuery);
        var url = $"https://www.unimarc.cl/search?q={encodedQuery}";
        Console.WriteLine($"[UNIMARC] Consultando tienda: {url}");

        return await _browser.GetHtmlAsync(url, cancellationToken, waitAfterLoadMs: 5000, keepPageOpenMs: 0);
    }
}