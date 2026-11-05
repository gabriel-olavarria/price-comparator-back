using System.Text.Json;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceComparator.Application.DTOs.Extractions;
using PriceComparator.Application.Interfaces.Persistence;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Infrastructure.Persistence.Configurations;

namespace PriceComparator.Infrastructure.ProductOffers.Tottus;

public sealed class TottusProductOfferSearcher(
    HttpClient httpClient,
    TottusProductParser parser,
    TottusCategoryParser categoryParser,
    TottusCategoryResolver categoryResolver,
    ILogger<TottusProductOfferSearcher> logger,
    IExtractedSearcherRepository extractedSearchRepository,
    IProcessedSearchRepository processedSearchRepository,
    IOptions<SupermarketExtractionOptions> options)
    : IProductOfferSearcher, ITransientDependency
{

    private const string CategoryCatalogUrl = "/tottus-cl/lista/CATG27292/Arroz";
    private readonly SupermarketExtractionOptions _options = options.Value;
    public string StoreCode => "Tottus";

    public async Task<(IReadOnlyCollection<Product> Products, bool FromCache)> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim();
        // 1. Consultar productos procesados solamente
        // cuando Force está deshabilitado.
        if (!_options.Force)
        {
            var processedSearch = await processedSearchRepository.FindTodayAsync(StoreCode, normalizedQuery, cancellationToken);

            if (processedSearch is not null && processedSearch.Products.Count > 0)
            {
                Console.WriteLine($"[TOTTUS] Usando productos almacenados: " + $"{processedSearch.ProductCount}");
                return (processedSearch.Products, true);
            }
        }
        else
        {
            logger.LogInformation("[TOTTUS] Force habilitado. Se realizará una nueva extracción para {Query}.", normalizedQuery);
        }
        
        var catalogHtml = await GetHtmlAsync(CategoryCatalogUrl, cancellationToken);
        var categories = categoryParser.Parse(catalogHtml);
        var candidates = categoryResolver.Resolve(categories, query);
        var rawProducts = new List<JsonElement>();
        var allOffers = new List<Product>();
        foreach (var category in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var categoryHtml = IsSameUrl(CategoryCatalogUrl, category.Url) ? catalogHtml : await GetHtmlAsync(category.Url, cancellationToken);
                var rawJson = await parser.ExtractRawJsonAsync(categoryHtml, cancellationToken);
                var offers = await parser.ParseAsync(categoryHtml, cancellationToken);

                Console.WriteLine($"[TOTTUS] Productos encontrados en {category.Name}: {offers.Count}");

                if (offers.Count == 0)
                {
                    Console.WriteLine("[TOTTUS] Categoría sin productos. Probando siguiente candidato.");
                    continue;
                }

                using var document = JsonDocument.Parse(rawJson);
                rawProducts.AddRange(document.RootElement.EnumerateArray().Select(x => x.Clone()));
                allOffers.AddRange(offers);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    $"[TOTTUS] Error consultando categoría {category.Name}: " +
                    $"{exception.GetType().Name} - {exception.Message}");
            }
        }
        
        if (allOffers.Count == 0)
        {
            Console.WriteLine($"[TOTTUS] Ninguna categoría candidata entregó productos para: {query}");
            return ([], false);
        }
        //Guardar el JSON original en Neon.
        await SaveExtracted(JsonSerializer.Serialize(rawProducts), query, cancellationToken);
        return (allOffers, false);
    }

    private async Task<string> GetHtmlAsync(string url, CancellationToken cancellationToken) {
       
        Console.WriteLine($"[TOTTUS] Consultando: {url}");
        using var response = await httpClient.GetAsync(url, cancellationToken);
        Console.WriteLine($"[TOTTUS] HTTP {(int)response.StatusCode} " + $"{response.StatusCode}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private bool IsSameUrl(string firstUrl, string secondUrl)
    {
        var first = new Uri(httpClient.BaseAddress!, firstUrl);
        var second = new Uri(httpClient.BaseAddress!, secondUrl);
        return first == second;
    }
    
    public async Task SaveExtracted(string rawJson, string normalizedQuery, CancellationToken cancellationToken)
    {
        await extractedSearchRepository.SaveExtractedAsync(StoreCode, normalizedQuery, rawJson, cancellationToken);
    }

    public async Task SaveProcessed(IReadOnlyCollection<Product> products, string query, CancellationToken cancellationToken = default)
    {
        await processedSearchRepository.SaveProcessedAsync(StoreCode, query, products, cancellationToken);
    }
}