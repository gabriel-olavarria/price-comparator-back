using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PriceComparator.Application.DTOs.Extractions;
using PriceComparator.Application.Interfaces.Persistence;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Persistence.Contexts;

namespace PriceComparator.Infrastructure.Persistence.Repositories;

public sealed class ExtractedSearcherRepository(
    PriceComparatorDbContext context,
    IUnitOfWork unitOfWork,
    ILogger<ExtractedSearcherRepository> logger)
    :
        Repository<ExtractedSearch, long>(context),
        IExtractedSearcherRepository
{
    private static readonly TimeZoneInfo ChileTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
    private readonly ILogger<ExtractedSearcherRepository> _logger = logger;

    public async Task<ExtractedSearch?> FindByStoreAndQueryAsync(string store, string query, CancellationToken cancellationToken = default)
    {
        
        return await context.Set<ExtractedSearch>()
            .FirstOrDefaultAsync(x =>
                    x.Store == store &&
                    x.SearchQuery == query ,
                cancellationToken);
    }
    
    public async Task SaveExtractedAsync(string store, string query, string rawJson, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim();
        var document = JsonDocument.Parse(rawJson);
        var productCount = document.RootElement.GetArrayLength();
        
        if (productCount == 0)
            return;

        var extracted = await FindByStoreAndQueryAsync(store, normalizedQuery, cancellationToken);

        if (extracted is null)
        {
            extracted = new ExtractedSearch
            {
                Store = store,
                SearchQuery = normalizedQuery
            };

            await AddAsync(extracted, cancellationToken);
        }

        extracted.Products = document;
        extracted.ProductCount = productCount;
        extracted.ExtractedAt = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("[{store}] Extracción RAW guardada en PostgreSQL.", store);
    }
    
}
