using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Persistence;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Persistence.Configurations;

namespace PriceComparator.Infrastructure.ProductOffers.Lider;

public sealed class LiderProductOfferSearcher(
    LiderGraphQLClient graphqlClient,
    LiderProductParser parser,
    IExtractedSearchRepository extractedSearchRepository,
    IProcessedSearchRepository processedSearchRepository,
    ILogger<LiderProductOfferSearcher> logger,
    IOptions<SupermarketExtractionOptions> options)
    : IProductOfferSearcher, ITransientDependency
{
    public string StoreCode => "Lider";
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
                logger.LogInformation("[LIDER] Usando {Count} productos almacenados.", processedSearch.Products.Count);
                return (processedSearch.Products, true);
            }
        }
        else
        {
            logger.LogInformation("[LIDER] Force habilitado. Se realizará una nueva extracción para {Query}.", normalizedQuery);
        }
        

        var response = await graphqlClient.SearchAsync(normalizedQuery, page: 1, cancellationToken: cancellationToken);
        var (rawJson, products) = parser.ParseGraphQL(response.Body, cancellationToken);

        //Guardar el JSON original en Neon.
        await SaveExtracted(rawJson, normalizedQuery, cancellationToken);
        
        return (products, false);
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