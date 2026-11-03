
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PriceComparator.Application.DTOs.Extractions;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Persistence;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Persistence.Contexts;

namespace PriceComparator.Infrastructure.Persistence.Repositories;

public sealed class ProcessedSearchRepository(
    PriceComparatorDbContext context,
    IUnitOfWork unitOfWork,
    ILogger<ProcessedSearchRepository> logger)
    : IProcessedSearchRepository, IScopedDependency
{
    private static readonly TimeZoneInfo ChileTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");

    public async Task<ProcessedSearch?> FindTodayAsync(string store, string query, CancellationToken cancellationToken = default)
    {
        var normalizedStore = Normalize(store);
        var normalizedQuery = Normalize(query);

        var (startUtc, endUtc) = GetTodayUtcRange();

        var entity = await context.Set<ProcessedSearch>()
            .AsNoTracking()
            .Where(x =>
                x.Store == normalizedStore &&
                x.SearchQuery == normalizedQuery &&
                x.ProductCount > 0 &&
                x.GeneratedAt >= startUtc &&
                x.GeneratedAt < endUtc)
            .OrderByDescending(x => x.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null || entity.Products.Count == 0)
            return null;

        return entity;
    }

    public async Task SaveAsync(ProcessedTodayDto data, CancellationToken cancellationToken = default)
    {
        if (data.Products.Count == 0)
            return;

        var store = Normalize(data.Store);
        var query = Normalize(data.Query);

        var entity = await context.Set<ProcessedSearch>()
            .FirstOrDefaultAsync(
                x => x.Store == store &&
                     x.SearchQuery == query,
                cancellationToken);

        if (entity is null)
        {
            entity = new ProcessedSearch
            {
                Store = store,
                SearchQuery = query,
                Products = data.Products,
                ProductCount = data.Products.Count,
                GeneratedAt = DateTime.UtcNow
            };

            await context.Set<ProcessedSearch>().AddAsync(entity, cancellationToken);
        }
        else
        {
            entity.Products = data.Products;
            entity.ProductCount = data.Products.Count;
            entity.GeneratedAt = DateTime.UtcNow;

            // Asegurar que EF Core persista la colección JSON.
            context.Entry(entity)
                .Property(x => x.Products)
                .IsModified = true;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Productos procesados guardados en Neon. Store={Store}, Query={Query}, Count={Count}",
            store,
            query,
            data.Products.Count);
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static (DateTime StartUtc, DateTime EndUtc)
        GetTodayUtcRange()
    {
        var nowChile = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            ChileTimeZone);

        var startChile = nowChile.Date;
        var endChile = startChile.AddDays(1);

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(
            startChile,
            ChileTimeZone);

        var endUtc = TimeZoneInfo.ConvertTimeToUtc(
            endChile,
            ChileTimeZone);

        return (startUtc, endUtc);
    }
}
