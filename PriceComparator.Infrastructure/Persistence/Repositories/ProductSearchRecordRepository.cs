using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Persistence.Contexts;
using Npgsql;
using NpgsqlTypes;

namespace PriceComparator.Infrastructure.Persistence.Repositories;


public sealed class ProductSearchRecordRepository(PriceComparatorDbContext context) 
    : Repository<ProductSearchRecord, long>(context),
        IProductSearchRecordRepository
{
    public async Task<List<ProductSearchRecord>> GetByStoreAndQueryAsync(string store, string query, CancellationToken cancellationToken = default)
    {
        return await context.Set<ProductSearchRecord>()
            .Where(x => x.Store == store && x.Query == query)
            .ToListAsync(cancellationToken);
    }

    public async Task AddIfNotExistsAsync(
        IReadOnlyCollection<ProductSearchRecord> records,
        CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
            return;

        var json = JsonSerializer.Serialize(records.Select(x => new
        {
            x.Store,
            x.Name,
            x.Query,
            x.CanonicalKey,
            x.CanonicalName,
            x.SearchDate
        }));

        const string sql = """
                           INSERT INTO "PriceComparator"."ProductSearchRecord"
                               ("Store", "Name", "Query",
                                "CanonicalKey", "CanonicalName", "SearchDate")
                           SELECT
                               item."Store",
                               item."Name",
                               item."Query",
                               item."CanonicalKey",
                               item."CanonicalName",
                               item."SearchDate"
                           FROM jsonb_to_recordset({0}::jsonb) AS item(
                               "Store" text,
                               "Name" text,
                               "Query" text,
                               "CanonicalKey" text,
                               "CanonicalName" text,
                               "SearchDate" timestamp with time zone
                           )
                           ON CONFLICT ("Store", "Name", "Query")
                           DO NOTHING;
                           """;

        await context.Database.ExecuteSqlRawAsync(sql, new object[] { json }, cancellationToken);
    }
}