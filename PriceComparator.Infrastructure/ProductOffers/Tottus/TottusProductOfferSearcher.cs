using System.Text.Json;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using Microsoft.Extensions.Logging;
using PriceComparator.Application.DTOs.Extractions;
using PriceComparator.Application.Interfaces.Repositories;

namespace PriceComparator.Infrastructure.ProductOffers.Tottus;

public sealed class TottusProductOfferSearcher : IProductOfferSearcher, ITransientDependency
{

    private const string CategoryCatalogUrl = "/tottus-cl/lista/CATG27292/Arroz";
    private readonly HttpClient _httpClient;
    private readonly TottusProductParser _parser;
    private readonly TottusCategoryParser _categoryParser;
    private readonly TottusCategoryResolver _categoryResolver;
    private readonly ILogger<TottusProductOfferSearcher> _logger;
    private readonly IExtractedSearcherRepository _extractedSearcherRepository;
    private readonly IProcessedSearchRepository _processedSearchRepository;

    public string StoreCode => "Tottus";

    public TottusProductOfferSearcher(HttpClient httpClient, 
        TottusProductParser parser, 
        TottusCategoryParser categoryParser, 
        TottusCategoryResolver categoryResolver,
        ILogger<TottusProductOfferSearcher> logger,
        IExtractedSearcherRepository extractedSearcherRepository,
        IProcessedSearchRepository processedSearchRepository)
    {
        _httpClient = httpClient;
        _parser = parser;
        _categoryParser = categoryParser;
        _categoryResolver = categoryResolver;
        _logger = logger;
        _extractedSearcherRepository = extractedSearcherRepository;
        _processedSearchRepository = processedSearchRepository;
    }

    public async Task<(IReadOnlyCollection<Product> Products, bool FromCache)> SearchAsync(string query, CancellationToken cancellationToken = default)
    {

        var normalizedQuery = query.Trim();
        var processedSearch = await _processedSearchRepository.FindTodayAsync(StoreCode, normalizedQuery, cancellationToken);

        if (processedSearch is not null && processedSearch.Products.Count > 0)
        {
            Console.WriteLine($"[TOTTUS] Usando productos almacenados: " + $"{processedSearch.ProductCount}");
            return (processedSearch.Products, true);
        }
        
        var catalogHtml = await GetHtmlAsync(CategoryCatalogUrl, cancellationToken);
        var categories = _categoryParser.Parse(catalogHtml);
        var candidates = _categoryResolver.Resolve(categories, query);
        

        foreach (var category in candidates) {
           
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var categoryHtml = IsSameUrl(CategoryCatalogUrl, category.Url) ? catalogHtml : await GetHtmlAsync(category.Url, cancellationToken);
              
                //Almacenar entrada de datos
                await SaveExtracted(categoryHtml, query, cancellationToken);
                
                var offers = await _parser.ParseAsync(categoryHtml, cancellationToken);
                Console.WriteLine($"[TOTTUS] Productos encontrados en " + $"{category.Name}: {offers.Count}");

                if (offers.Count == 0)
                {
                    Console.WriteLine($"[TOTTUS] Categoría sin productos. " + $"Probando siguiente candidato.");
                    continue;
                }
                
                return (offers, false);
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
        return ([], false);
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
            "[TOTTUS] {Count} productos procesados guardados en PostgreSQL. Query: {Query}",
            products.Count,
            normalizedQuery);
    }



}