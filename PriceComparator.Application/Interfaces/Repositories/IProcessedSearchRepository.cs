using PriceComparator.Application.DTOs.Extractions;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Interfaces.Repositories;
public interface IProcessedSearchRepository
{
    Task<ProcessedSearch?> FindTodayAsync(
        string store,
        string query,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        ProcessedTodayDto data,
        CancellationToken cancellationToken = default);
}