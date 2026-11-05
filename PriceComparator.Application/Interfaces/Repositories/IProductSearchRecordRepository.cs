using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Domain.Entities;

public interface IProductSearchRecordRepository 
    : IRepository<ProductSearchRecord, long>
{
    Task<List<ProductSearchRecord>> GetByStoreAndQueryAsync(string store, string query, CancellationToken cancellationToken = default);
    Task AddIfNotExistsAsync(IReadOnlyCollection<ProductSearchRecord> records, CancellationToken cancellationToken = default);
}