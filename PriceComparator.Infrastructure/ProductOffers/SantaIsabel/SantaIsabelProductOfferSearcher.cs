using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Storage;

namespace PriceComparator.Infrastructure.ProductOffers.SantaIsabel;

public sealed class SantaIsabelProductOfferSearcher : IProductOfferSearcher, ITransientDependency
{
    private readonly HttpClient _httpClient;
    private readonly SantaIsabelProductParser _parser;
    private readonly JsonProductStore _productStore;
    public string StoreCode => "SantaIsabel";

    public SantaIsabelProductOfferSearcher(HttpClient httpClient, SantaIsabelProductParser parser, JsonProductStore productStore)
    {
        _httpClient = httpClient;
        _parser = parser;
        _productStore = productStore;
    }

    public async Task<IReadOnlyCollection<Product>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {

        var normalizedQuery = query.Trim();
        var storedOffers = await _productStore.GetAsync(StoreCode, normalizedQuery, cancellationToken);
        
        if (storedOffers is null)
        {
            Console.WriteLine("[SANTA ISABEL] No hay datos almacenados vigentes. Consultando Santa Isabel.");
            return [];
        }

        var html = await SearchLiveAsync(normalizedQuery, cancellationToken);
        
        //Almacenar entrada de datos
        await SaveJson(html, normalizedQuery, cancellationToken);
        
        var offers = await _parser.ParseAsync(html, cancellationToken);
        Console.WriteLine($"[SANTA ISABEL] Productos encontrados: {offers.Count}");
       
        return offers;
    }

    public async Task SaveProducts(IReadOnlyCollection<Product> products, string query, CancellationToken cancellationToken = default)
    {
        await _productStore.SaveAsync(StoreCode, query, products, cancellationToken);
        Console.WriteLine($"[SANTA ISABEL] {products.Count} productos guardados en JSON.");
     
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
        var requestUrl = $"/busqueda?ft={encodedQuery}";
       
        Console.WriteLine($"[SANTA ISABEL] Consultando: {requestUrl}");
        using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
       
        Console.WriteLine($"[SANTA ISABEL] HTTP {(int)response.StatusCode} {response.StatusCode}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}