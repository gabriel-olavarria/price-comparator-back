
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PriceComparator.Application.Interfaces.Persistence;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Persistence.Contexts;

namespace PriceComparator.Infrastructure.Persistence.Repositories;

public sealed class ProcessedSearchRepository(
    PriceComparatorDbContext context,
    ILogger<ProcessedSearchRepository> logger,
    IUnitOfWork unitOfWork)
    : Repository<ProcessedSearch, long>(context), 
        IProcessedSearchRepository
{
    private static readonly TimeZoneInfo ChileTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");

    public async Task<ProcessedSearch?> FindTodayAsync(string store, string query, CancellationToken cancellationToken = default)
    {
        var normalizedStore = store.Trim();
        var normalizedQuery = query.Trim();
        
        var entity = await context.Set<ProcessedSearch>()
            .Where(x =>
                x.Store == normalizedStore &&
                x.SearchQuery == normalizedQuery)
            .OrderByDescending(x => x.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null || entity.Products.Count == 0)
            return null;

        return entity;
    }
    public async Task SaveProcessedAsync(string store, string query, IReadOnlyCollection<Product> products, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim();

        var processed = await FindTodayAsync(store, normalizedQuery, cancellationToken);

        if (processed is null)
        {
            processed = new ProcessedSearch
            {
                Store = store,
                SearchQuery = normalizedQuery
            };

            await AddAsync(processed, cancellationToken);
        }

        processed.Products = products;
        processed.ProductCount = products.Count;
        processed.GeneratedAt = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("[{store}] {Count} productos procesados guardados en PostgreSQL. Query: {Query}", store.ToUpper(), products.Count, normalizedQuery);

    }
 
  
   
}
