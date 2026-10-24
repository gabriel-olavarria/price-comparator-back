
using System.Text.Json;
using Microsoft.Playwright;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Browsers;
using PriceComparator.Infrastructure.Storage;

namespace PriceComparator.Infrastructure.ProductOffers.Unimarc;

public sealed class UnimarcProductOfferSearcher : IProductOfferSearcher, ITransientDependency
{
    private const string SearchEndpoint =
        "https://bff-unimarc-ecommerce.unimarc.cl/catalog/product/search";

    private readonly PlaywrightHtmlBrowser _browser;
    private readonly UnimarcProductJsonParser _parser;
    private readonly JsonProductStore _productStore;

    public string StoreCode => "Unimarc";

    public UnimarcProductOfferSearcher(PlaywrightHtmlBrowser browser, UnimarcProductJsonParser parser, JsonProductStore productStore)
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
            Console.WriteLine($"[UNIMARC] Usando productos almacenados: {storedOffers.Count}");
            return storedOffers;
        }

        // 2. Obtener productos desde JSON.
        var offers = await SearchJsonAsync(normalizedQuery, cancellationToken);

        return offers;
    }

    private async Task<IReadOnlyCollection<Product>> SearchJsonAsync(string query, CancellationToken cancellationToken)
    {
        var url = BuildSearchUrl(query);

        try
        {
            Console.WriteLine("[UNIMARC] Capturando JSON mediante Playwright...");
            var json = await _browser.GetJsonResponseAsync(url, SearchEndpoint, cancellationToken);
            Console.WriteLine($"[UNIMARC] JSON recibido: {json.Length} caracteres.");
            
            //Almacenar entrada de datos
            await SaveJson(json, query, cancellationToken);
            
            var products = _parser.Parse(json);
            Console.WriteLine($"[UNIMARC] Productos obtenidos desde JSON: {products.Count}");
            return products;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or PlaywrightException)
        {
            Console.WriteLine($"[UNIMARC] Error procesando JSON: {ex.Message}");
            return [];
        }
    }

    public Task<IReadOnlyCollection<Product>?> GetProcessedProductsAsync(string query, CancellationToken cancellationToken = default)
    {
        return _productStore.GetAsync(StoreCode, query, cancellationToken);
    }

    
    private static string BuildSearchUrl(string query)
    {
        var normalizedQuery = query.Trim().Replace(" ", "-");
        var encodedQuery = Uri.EscapeDataString(normalizedQuery);

        return $"https://www.unimarc.cl/search?q={encodedQuery}";
    }
    
    public async Task SaveProducts(IReadOnlyCollection<Product> products, string query, CancellationToken cancellationToken = default)
    {
        await _productStore.SaveAsync(StoreCode, query, products, cancellationToken);
        Console.WriteLine($"[TOTTUS] {products.Count} productos guardados en JSON.");
     
    }
    private async Task SaveJson(string json, string query, CancellationToken cancellationToken)
    {
        if (json.Length > 0)
        {
            await _productStore.SaveRawAsync(StoreCode, query, json, cancellationToken);
        }
    }
  

}
