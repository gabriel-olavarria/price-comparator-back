
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PriceComparator.Application.DTOs.Extractions;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Infrastructure.ProductOffers.Lider;

public sealed class LiderProductOfferSearcher
    : IProductOfferSearcher, ITransientDependency
{
    private readonly LiderGraphQLClient _graphqlClient;
    private readonly LiderProductParser _parser;
    private readonly IExtractedSearcherRepository _extractedRepository;
    private readonly IProcessedSearchRepository _processedRepository;
    private readonly ILogger<LiderProductOfferSearcher> _logger;

    public string StoreCode => "Lider";

    public LiderProductOfferSearcher(
        LiderGraphQLClient graphqlClient,
        LiderProductParser parser,
        IExtractedSearcherRepository extractedRepository,
        IProcessedSearchRepository processedRepository,
        ILogger<LiderProductOfferSearcher> logger)
    {
        _graphqlClient = graphqlClient;
        _parser = parser;
        _extractedRepository = extractedRepository;
        _processedRepository = processedRepository;
        _logger = logger;
    }

    public async Task<(IReadOnlyCollection<Product> Products, bool FromCache)> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim();

        var cached = await _processedRepository.FindTodayAsync(
            StoreCode,
            normalizedQuery,
            cancellationToken);

        if (cached is { Products.Count: > 0 })
        {
            _logger.LogInformation(
                "[LIDER] Usando {Count} productos almacenados.",
                cached.Products.Count);

            return (cached.Products, true);
        }

        var response = await _graphqlClient.SearchAsync(
            normalizedQuery,
            page: 1,
            cancellationToken: cancellationToken);

        var (rawJson, products) = _parser.ParseGraphQL(
            response.Body,
            cancellationToken);

        using var document = JsonDocument.Parse(rawJson);
        var rawCount = document.RootElement.GetArrayLength();

        if (rawCount > 0)
        {
            await _extractedRepository.SaveAsync(
                new RawExtractionDto(
                    Store: StoreCode,
                    Query: normalizedQuery,
                    RawJson: rawJson,
                    ProductCount: rawCount,
                    ExtractedAt: DateTimeOffset.UtcNow),
                cancellationToken);
        }

        _logger.LogInformation(
            "[LIDER] Extracción completada. RAW: {RawCount}, productos válidos: {ProductCount}.",
            rawCount,
            products.Count);

        return (products, false);
    }

    public async Task SaveProcessed(IReadOnlyCollection<Product> products, string query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var normalizedQuery = query.Trim();

        await _processedRepository.SaveAsync(
            new ProcessedTodayDto(
                Store: StoreCode,
                Query: normalizedQuery,
                ProductCount: products.Count,
                ProcessedAt: DateTime.UtcNow,
                Products: products),
            cancellationToken);

        _logger.LogInformation(
            "[LIDER] {Count} productos procesados guardados. Query: {Query}",
            products.Count,
            normalizedQuery);
    }
}
