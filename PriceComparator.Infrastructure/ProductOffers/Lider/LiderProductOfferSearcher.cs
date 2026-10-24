using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Browsers;
using PriceComparator.Infrastructure.Storage;

namespace PriceComparator.Infrastructure.ProductOffers.Lider;

public sealed class LiderProductOfferSearcher : IProductOfferSearcher, ITransientDependency
{
    private readonly PlaywrightHtmlBrowser _browser;
    private readonly LiderProductParser _parser;
    private readonly JsonProductStore _productStore;

    public string StoreCode => "Lider";

    public LiderProductOfferSearcher(PlaywrightHtmlBrowser browser, LiderProductParser parser, JsonProductStore productStore)
    {
        _browser = browser;
        _parser = parser;
        _productStore = productStore;
    }

    public async Task<IReadOnlyCollection<Product>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {

        var normalizedQuery = query.Trim();
        
        // 1. Consultar productos procesados almacenados.
        var storedOffers = await _productStore.GetAsync(StoreCode, normalizedQuery, cancellationToken);

        if (storedOffers is not null)
        {
            Console.WriteLine($"[LIDER] Usando productos almacenados: {storedOffers.Count}");
            return storedOffers;
        }

        var html = await SearchLiveAsync(normalizedQuery, cancellationToken);
        
        //Almacenar entrada de datos
        await SaveJson(html, normalizedQuery, cancellationToken);

        
        var offers = await _parser.ParseAsync(html, cancellationToken);
        Console.WriteLine($"[LIDER] Productos encontrados: {offers.Count}");

        return offers;
    }

    public async Task SaveProducts(IReadOnlyCollection<Product> products, string query, CancellationToken cancellationToken = default)
    {
        await _productStore.SaveAsync(StoreCode, query, products, cancellationToken);
        Console.WriteLine($"[LIDER] {products.Count} productos guardados en JSON.");
     
    }
    
    public Task<IReadOnlyCollection<Product>?> GetProcessedProductsAsync(string query, CancellationToken cancellationToken = default)
    {
        return _productStore.GetAsync(StoreCode, query, cancellationToken);
    }
    
    private async Task SaveJson(string html, string normalizedQuery, CancellationToken cancellationToken)
    {
        var rawJson = await _parser.ExtractRawJsonAsync(html, cancellationToken);

        if (rawJson != "[]")
        {
            await _productStore.SaveRawAsync(StoreCode, normalizedQuery, rawJson, cancellationToken);
        }
    }
    
    
    private async Task<string> SearchLiveAsync(string query, CancellationToken cancellationToken)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var url = $"https://super.lider.cl/search?q={encodedQuery}";

        Console.WriteLine($"[LIDER] Consultando tienda: {url}");

        return await _browser.GetHtmlAsync(url, cancellationToken, waitAfterLoadMs: 0, keepPageOpenMs: 5000);
    }
}