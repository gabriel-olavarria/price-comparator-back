
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PriceComparator.Application.DTOs.Extractions;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Persistence;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Persistence.Contexts;

namespace PriceComparator.Infrastructure.Persistence.Repositories;

public sealed class ExtractedSearcherRepository :
    IExtractedSearcherRepository,
    IScopedDependency
{
    private static readonly TimeZoneInfo ChileTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
    private readonly PriceComparatorDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExtractedSearcherRepository> _logger;

    public ExtractedSearcherRepository(
        PriceComparatorDbContext context,
        IUnitOfWork unitOfWork,
        ILogger<ExtractedSearcherRepository> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ExtractedTodayDto?> FindTodayAsync(
        string store,
        string query,
        CancellationToken cancellationToken = default)
    {
        var normalizedStore = Normalize(store);
        var normalizedQuery = Normalize(query);

        var nowUtc = DateTime.UtcNow;

        var chileToday = TimeZoneInfo
            .ConvertTimeFromUtc(nowUtc, ChileTimeZone)
            .Date;

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(
            chileToday,
            ChileTimeZone);

        var endUtc = TimeZoneInfo.ConvertTimeToUtc(
            chileToday.AddDays(1),
            ChileTimeZone);

        var extraction = await _context.Set<ExtractedSearch>()
            .AsNoTracking()
            .Where(x =>
                x.Store == normalizedStore &&
                x.SearchQuery == normalizedQuery &&
                x.ProductCount > 0 &&
                x.ExtractedAt >= startUtc &&
                x.ExtractedAt < endUtc)
            .Select(x => new
            {
                x.Store,
                x.SearchQuery,
                x.ProductCount,
                x.ExtractedAt,
                x.Products
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (extraction is null)
            return null;

        return new ExtractedTodayDto(
            extraction.Store,
            extraction.SearchQuery,
            extraction.ProductCount,
            extraction.ExtractedAt,
            extraction.Products.RootElement.GetRawText()
        );
    }
    
    public async Task SaveAsync(RawExtractionDto data, CancellationToken cancellationToken = default)
    {

        var store = Normalize(data.Store);
        var query = Normalize(data.Query);

        if (string.IsNullOrWhiteSpace(data.RawJson))
            throw new ArgumentException(
                "El JSON original no puede estar vacío.",
                nameof(data));

        using var document = JsonDocument.Parse(data.RawJson);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new ArgumentException(
                "El JSON original debe ser un arreglo.",
                nameof(data));

        var productCount = document.RootElement.GetArrayLength();

        if (productCount == 0)
            throw new InvalidOperationException(
                "No se puede guardar una extracción sin productos.");

        if (data.ProductCount != productCount)
            throw new InvalidOperationException(
                "La cantidad de productos no coincide con el JSON.");

        if (data.ExtractedAt.Offset != TimeSpan.Zero)
            throw new ArgumentException(
                "ExtractedAt debe estar expresado en UTC.",
                nameof(data));

        var extractedAt = data.ExtractedAt.UtcDateTime;

        var extraction = await _context.Set<ExtractedSearch>()
            .FirstOrDefaultAsync(
                x => x.Store == store &&
                     x.SearchQuery == query,
                cancellationToken);

        if (extraction is null)
        {
            extraction = new ExtractedSearch
            {
                Store = store,
                SearchQuery = query,
                Products = JsonDocument.Parse(data.RawJson),
                ProductCount = productCount,
                ExtractedAt = extractedAt
            };

            await _context.Set<ExtractedSearch>()
                .AddAsync(extraction, cancellationToken);
        }
        else
        {
            extraction.Products = JsonDocument.Parse(data.RawJson);
            extraction.ProductCount = productCount;
            extraction.ExtractedAt = extractedAt;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Extracción guardada en Neon. Store: {Store}, " +
            "Query: {Query}, Products: {ProductCount}, " +
            "ExtractedAt: {ExtractedAt}",
            store,
            query,
            productCount,
            extractedAt);
    }

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return value.Trim().ToLowerInvariant();
    }
}
