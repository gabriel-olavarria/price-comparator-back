using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Interfaces.Repositories;
public interface IProcessedSearchRepository : IRepository<ProcessedSearch, long>
{
    Task<ProcessedSearch?> FindTodayAsync(string store, string query, CancellationToken cancellationToken = default);

    Task SaveProcessedAsync(string store, string query, IReadOnlyCollection<Product> products, CancellationToken cancellationToken = default);

}