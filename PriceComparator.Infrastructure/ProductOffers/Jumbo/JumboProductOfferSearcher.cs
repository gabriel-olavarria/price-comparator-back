using System.Text.Json;
using Microsoft.Extensions.Logging;
using PriceComparator.Application.DTOs.Extractions;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;


namespace PriceComparator.Infrastructure.ProductOffers.Jumbo;

public sealed class JumboProductOfferSearcher : IProductOfferSearcher, ITransientDependency
{
    private readonly HttpClient _httpClient;
    private readonly JumboProductParser _parser;
    private readonly ILogger<JumboProductOfferSearcher> _logger;
    private readonly IExtractedSearcherRepository _extractedSearcherRepository;
    private readonly IProcessedSearchRepository _processedSearchRepository;
    
    public string StoreCode => "Jumbo";

    public JumboProductOfferSearcher(HttpClient httpClient, JumboProductParser parser,  IExtractedSearcherRepository extractedSearcherRepository,  IProcessedSearchRepository processedSearchRepository, ILogger<JumboProductOfferSearcher> logger)
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
            Console.WriteLine($"[JUMBO] Usando productos almacenados: " + $"{processedSearch.ProductCount}");
            return (processedSearch.Products, true);
        }
        
        Console.WriteLine("[JUMBO] No hay datos almacenados vigentes. " + "Consultando Jumbo.");
        var html = await SearchLiveAsync(normalizedQuery, cancellationToken);
        await SaveExtracted(html, normalizedQuery, cancellationToken);
       
        var offers = await _parser.ParseAsync(html, cancellationToken);
 
        Console.WriteLine($"[JUMBO] Productos encontrados: {offers.Count}");
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
        using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
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
            "[JUMBO] {Count} productos procesados guardados en PostgreSQL. Query: {Query}",
            products.Count,
            normalizedQuery);
    }
    
}