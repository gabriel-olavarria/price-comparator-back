using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceComparator.Infrastructure.Persistence.Configurations;

namespace PriceComparator.Infrastructure.ProductOffers.SantaIsabel;

public sealed class SantaIsabelProductOfferSearcher(
    HttpClient httpClient,
    SantaIsabelProductParser parser,
    IExtractedSearchRepository extractedSearchRepository,
    IProcessedSearchRepository processedSearchRepository,
    ILogger<SantaIsabelProductOfferSearcher> logger,
    IOptions<SupermarketExtractionOptions> options)
    : IProductOfferSearcher, ITransientDependency
{
    public string StoreCode => "SantaIsabel";
    private readonly SupermarketExtractionOptions _options = options.Value;

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
                Console.WriteLine($"[SANTA ISABEL] Usando productos almacenados: " + $"{processedSearch.ProductCount}");
                return (processedSearch.Products, true);
            }
        }
        else
        {
            logger.LogInformation("[SANTA ISABEL] Force habilitado. Se realizará una nueva extracción para {Query}.", normalizedQuery);
        }

        var html = await SearchLiveAsync(normalizedQuery, cancellationToken);
        var rawJson = await parser.ExtractRawJsonAsync(html, cancellationToken);
        //Guardar el JSON original en Neon.
        await SaveExtracted(rawJson, normalizedQuery, cancellationToken);

        var offers = await parser.ParseAsync(html, cancellationToken);
        
        Console.WriteLine($"[SANTA ISABEL] Productos encontrados: {offers.Count}");

        return (offers, false);
    }

    private async Task<string> SearchLiveAsync(string query, CancellationToken cancellationToken)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var requestUrl = $"/busqueda?ft={encodedQuery}";

        Console.WriteLine($"[SANTA ISABEL] Consultando: {requestUrl}");
        using var response = await httpClient.GetAsync(requestUrl, cancellationToken);

        Console.WriteLine($"[SANTA ISABEL] HTTP {(int)response.StatusCode} {response.StatusCode}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
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