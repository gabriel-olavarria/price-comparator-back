using System.Text.Json;
using PriceComparator.Application.Interfaces;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Browsers;
using PriceComparator.Infrastructure.Storage;

namespace PriceComparator.Infrastructure.ProductOffers.Unimarc;

public sealed class UnimarcProductOfferSearcher : IProductOfferSearcher, ITransientDependency
{
    private const string SearchEndpoint = "https://bff-unimarc-ecommerce.unimarc.cl/catalog/product/search";
    private readonly PlaywrightHtmlBrowser _browser;
    private readonly UnimarcProductParser _htmlParser;
    private readonly UnimarcProductJsonParser _jsonParser;
    private readonly JsonProductStore _productStore;

    public string StoreCode => "Unimarc";

    public UnimarcProductOfferSearcher(PlaywrightHtmlBrowser browser, UnimarcProductParser htmlParser, UnimarcProductJsonParser jsonParser, JsonProductStore productStore)
    {
        _browser = browser;
        _htmlParser = htmlParser;
        _jsonParser = jsonParser;
        _productStore = productStore;
    }

    public async Task<IReadOnlyCollection<Product>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var normalizedQuery = query.Trim();

        // 1. Consultar caché antes de navegar.
        var storedOffers = await _productStore.GetAsync(StoreCode, normalizedQuery, cancellationToken);

        if (storedOffers is not null)
        {
            Console.WriteLine($"[UNIMARC] Usando productos almacenados: {storedOffers.Count}");
            return storedOffers;
        }

        Console.WriteLine("[UNIMARC] No hay datos almacenados vigentes. Consultando Unimarc.");

        // 2. Intentar obtener productos mediante JSON.
        var offers = await SearchJsonAsync(normalizedQuery, cancellationToken);

        // 3. Si no se obtuvieron productos, utilizar HTML.
        if (offers.Count == 0)
        {
            Console.WriteLine("[UNIMARC] Utilizando búsqueda HTML de respaldo.");
            offers = await SearchHtmlAsync(normalizedQuery, cancellationToken);
        }

        // 4. Guardar resultados.
        await _productStore.SaveAsync(StoreCode, normalizedQuery, offers, cancellationToken);
        Console.WriteLine($"[UNIMARC] {offers.Count} productos guardados en JSON.");
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

            var products = _jsonParser.Parse(json);

            Console.WriteLine($"[UNIMARC] Productos obtenidos desde JSON: {products.Count}");

            return products;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or Microsoft.Playwright.PlaywrightException)
        {
            Console.WriteLine($"[UNIMARC] Error procesando JSON: {ex.Message}");
            return [];
        }
    }

    private async Task<IReadOnlyCollection<Product>> SearchHtmlAsync(string query, CancellationToken cancellationToken)
    {
        var url = BuildSearchUrl(query);

        Console.WriteLine($"[UNIMARC] Consultando HTML: {url}");

        var html = await _browser.GetHtmlAsync(url, cancellationToken, waitAfterLoadMs: 5000, keepPageOpenMs: 0);
        return await _htmlParser.ParseAsync(html, cancellationToken);
    }

    private static string BuildSearchUrl(string query)
    {
        var normalizedQuery = query.Trim().Replace(" ", "-");
        var encodedQuery = Uri.EscapeDataString(normalizedQuery);
        return $"https://www.unimarc.cl/search?q={encodedQuery}";
    }
}