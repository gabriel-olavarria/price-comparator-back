using System.Text.Json;
using PriceComparator.Application.DTOs.Extractions;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace PriceComparator.Infrastructure.ProductOffers.SantaIsabel;

public sealed class SantaIsabelProductOfferSearcher : IProductOfferSearcher, ITransientDependency
{
    private readonly HttpClient _httpClient;
    private readonly SantaIsabelProductParser _parser;
    private readonly IExtractedSearcherRepository _extractedSearcherRepository;
    private readonly IProcessedSearchRepository _processedSearchRepository;
    private readonly ILogger<SantaIsabelProductOfferSearcher> _logger;
    
    public string StoreCode => "SantaIsabel";

    public SantaIsabelProductOfferSearcher(HttpClient httpClient, 
        SantaIsabelProductParser parser, 
        IExtractedSearcherRepository extractedSearcherRepository,  
        IProcessedSearchRepository processedSearchRepository,
        ILogger<SantaIsabelProductOfferSearcher> logger)
    {
        _httpClient = httpClient;
        _parser = parser;
        _extractedSearcherRepository = extractedSearcherRepository;
        _processedSearchRepository = processedSearchRepository;
        _logger = logger;
    }

    public async Task<(IReadOnlyCollection<Product> Products, bool FromCache)> SearchAsync(string query, CancellationToken cancellationToken = default)
    {

        var normalizedQuery = query.Trim();
        var processedSearch = await _processedSearchRepository.FindTodayAsync(StoreCode, normalizedQuery, cancellationToken);

        if (processedSearch is not null && processedSearch.Products.Count > 0)
        {
            Console.WriteLine($"[SANTA ISABEL] Usando productos almacenados: " + $"{processedSearch.ProductCount}");
            return (processedSearch.Products, true);
        }

        var html = await SearchLiveAsync(normalizedQuery, cancellationToken);
        
        //Almacenar entrada de datos
        await SaveExtracted(html, normalizedQuery, cancellationToken);
        
        var offers = await _parser.ParseAsync(html, cancellationToken);
        Console.WriteLine($"[SANTA ISABEL] Productos encontrados: {offers.Count}");
        
        return (offers, false);
    }

     
    private async Task SaveExtracted(string html, string normalizedQuery, CancellationToken cancellationToken)
    {
        var rawJson = await _parser.ExtractRawJsonAsync(html, cancellationToken);

        if (string.IsNullOrWhiteSpace(rawJson) || rawJson == "[]")
            return;

        using var document = JsonDocument.Parse(rawJson);

        var data = new RawExtractionDto(
            Store: StoreCode,
            Query: normalizedQuery,
            RawJson: rawJson,
            ProductCount: document.RootElement.GetArrayLength(),
            ExtractedAt: DateTimeOffset.UtcNow
        );

        await _extractedSearcherRepository.SaveAsync(data, cancellationToken);
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
    
    public async Task SaveProcessed(IReadOnlyCollection<Product> products, string query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim();

        var data = new ProcessedTodayDto(
            Store: StoreCode,
            Query: normalizedQuery,
            ProductCount: products.Count,
            ProcessedAt: DateTime.UtcNow,
            Products: products
        );

        await _processedSearchRepository.SaveAsync(data, cancellationToken);

        _logger.LogInformation(
            "[SANTA ISABEL] {Count} productos procesados guardados en PostgreSQL. Query: {Query}",
            products.Count,
            normalizedQuery);
    }

}