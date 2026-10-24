using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Storage;

namespace PriceComparator.Infrastructure.ProductOffers.Jumbo;

public sealed class JumboProductOfferSearcher : IProductOfferSearcher, ITransientDependency
{
    private readonly HttpClient _httpClient;
    private readonly JumboProductParser _parser;
    private readonly JsonProductStore _productStore;
    public string StoreCode => "Jumbo";

    public JumboProductOfferSearcher(HttpClient httpClient, JumboProductParser parser, JsonProductStore productStore)
    {
        _httpClient = httpClient;
        _parser = parser;
        _productStore = productStore;
    }

    public async Task<IReadOnlyCollection<Product>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {

        var normalizedQuery = query.Trim();
        var storedOffers = await _productStore.GetAsync(StoreCode, normalizedQuery, cancellationToken);

        if (storedOffers is not null)
        {
            Console.WriteLine($"[JUMBO] Usando productos almacenados: " + $"{storedOffers.Count}");
            return storedOffers;
        }
        
        Console.WriteLine("[JUMBO] No hay datos almacenados vigentes. " + "Consultando Jumbo.");
        var html = await SearchLiveAsync(normalizedQuery, cancellationToken);
        await SaveJson(html, normalizedQuery, cancellationToken);
       
        var offers = await _parser.ParseAsync(html, cancellationToken);
 
        Console.WriteLine($"[JUMBO] Productos encontrados: {offers.Count}");
        return offers;
    }

    public async Task SaveProducts(IReadOnlyCollection<Product> products, string query, CancellationToken cancellationToken = default)
    {
        await _productStore.SaveAsync(StoreCode, query, products, cancellationToken);
        Console.WriteLine($"[JUMBO] {products.Count} productos guardados en JSON.");
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
        using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
    
}