using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Persistence;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Persistence.Configurations;


namespace PriceComparator.Infrastructure.ProductOffers.Jumbo;

public sealed class JumboProductOfferSearcher(
    HttpClient httpClient,
    JumboProductParser parser,
    IExtractedSearcherRepository extractedSearchRepository,
    IProcessedSearchRepository processedSearchRepository,
    ILogger<JumboProductOfferSearcher> logger,
    IOptions<SupermarketExtractionOptions> options)
    : IProductOfferSearcher, ITransientDependency
{
    public string StoreCode => "Jumbo";
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
                Console.WriteLine($"[JUMBO] Usando productos almacenados: " + $"{processedSearch.ProductCount}");
                return (processedSearch.Products, true);
            }
        }
        else
        {
            logger.LogInformation("[JUMBO] Force habilitado. Se realizará una nueva extracción para {Query}.", normalizedQuery);
        }
        
        Console.WriteLine("[JUMBO] No hay datos almacenados vigentes. " + "Consultando Jumbo.");
        var html = await SearchLiveAsync(normalizedQuery, cancellationToken);
        var rawJson = await parser.ExtractRawJsonAsync(html, cancellationToken);

        //Guardar el JSON original en Neon.
        await SaveExtracted(rawJson, normalizedQuery, cancellationToken);
       
        var offers = await parser.ParseAsync(html, cancellationToken);
 
        Console.WriteLine($"[JUMBO] Productos encontrados: {offers.Count}");
        return (offers, false);
    }
    private async Task<string> SearchLiveAsync(string query, CancellationToken cancellationToken)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var requestUrl = $"/busqueda?ft={encodedQuery}";
        using var response = await httpClient.GetAsync(requestUrl, cancellationToken);
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