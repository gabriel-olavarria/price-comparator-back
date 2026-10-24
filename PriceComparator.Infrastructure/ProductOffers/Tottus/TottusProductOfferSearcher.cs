using PriceComparator.Application.Interfaces;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Storage;

namespace PriceComparator.Infrastructure.ProductOffers.Tottus;

public sealed class TottusProductOfferSearcher : IProductOfferSearcher, ITransientDependency
{

    private const string CategoryCatalogUrl = "/tottus-cl/lista/CATG27292/Arroz";
    private readonly HttpClient _httpClient;
    private readonly TottusProductParser _parser;
    private readonly TottusCategoryParser _categoryParser;
    private readonly TottusCategoryResolver _categoryResolver;
    private readonly JsonProductStore _productStore;
    public string StoreCode => "Tottus";

    public TottusProductOfferSearcher(HttpClient httpClient, TottusProductParser parser, TottusCategoryParser categoryParser, TottusCategoryResolver categoryResolver, JsonProductStore productStore)
    {
        _httpClient = httpClient;
        _parser = parser;
        _categoryParser = categoryParser;
        _categoryResolver = categoryResolver;
        _productStore = productStore;
    }

    public async Task<IReadOnlyCollection<Product>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {

        query = query.Trim();
        var storedProducts = await _productStore.GetAsync(StoreCode, query, cancellationToken);

        if (storedProducts is not null)
        {
            Console.WriteLine($"[TOTTUS] Usando productos almacenados: {storedProducts.Count}");
            return storedProducts;
        }

        Console.WriteLine($"[TOTTUS] Resolviendo categoría para: {query}");
        var catalogHtml = await GetHtmlAsync(CategoryCatalogUrl, cancellationToken);
        var categories = _categoryParser.Parse(catalogHtml);
        var candidates = _categoryResolver.Resolve(categories, query);

        if (candidates.Count == 0)
        {
            Console.WriteLine($"[TOTTUS] No se encontraron categorías para: {query}");
            return [];
        }

        foreach (var category in candidates) {
           
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var categoryHtml = IsSameUrl(CategoryCatalogUrl, category.Url) ? catalogHtml : await GetHtmlAsync(category.Url, cancellationToken);
              
                //Almacenar entrada de datos
                await SaveJson(categoryHtml, query, cancellationToken);
                
                var offers = await _parser.ParseAsync(categoryHtml, cancellationToken);
                Console.WriteLine($"[TOTTUS] Productos encontrados en " + $"{category.Name}: {offers.Count}");

                if (offers.Count == 0)
                {
                    Console.WriteLine($"[TOTTUS] Categoría sin productos. " + $"Probando siguiente candidato.");
                    continue;
                }
                
                return offers;
            }
            catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[TOTTUS] Error consultando categoría " + $"{category.Name}: " + $"{exception.GetType().Name} - " + $"{exception.Message}");
            }
        }

        Console.WriteLine($"[TOTTUS] Ninguna categoría candidata entregó productos para: {query}");
        return [];
    }

    private async Task<string> GetHtmlAsync(string url, CancellationToken cancellationToken) {
       
        Console.WriteLine($"[TOTTUS] Consultando: {url}");
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        Console.WriteLine($"[TOTTUS] HTTP {(int)response.StatusCode} " + $"{response.StatusCode}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private bool IsSameUrl(string firstUrl, string secondUrl)
    {
        var first = new Uri(_httpClient.BaseAddress!, firstUrl);
        var second = new Uri(_httpClient.BaseAddress!, secondUrl);
        return first == second;
    }
    
    public Task<IReadOnlyCollection<Product>?> GetProcessedProductsAsync(string query, CancellationToken cancellationToken = default)
    {
        return _productStore.GetAsync(StoreCode, query, cancellationToken);
    }
    
    public async Task SaveProducts(IReadOnlyCollection<Product> products, string query, CancellationToken cancellationToken = default)
    {
        await _productStore.SaveAsync(StoreCode, query, products, cancellationToken);
        Console.WriteLine($"[TOTTUS] {products.Count} productos guardados en JSON.");
     
    }
    
    private async Task SaveJson(string html, string normalizedQuery, CancellationToken cancellationToken)
    {
        var rawJson = await _parser.ExtractRawJsonAsync(html, cancellationToken);

        if (rawJson != "[]")
        {
            await _productStore.SaveRawAsync(StoreCode, normalizedQuery, rawJson, cancellationToken);
        }
    }


}