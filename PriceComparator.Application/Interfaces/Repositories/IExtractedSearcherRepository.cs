using System.Text.Json;
using PriceComparator.Application.DTOs.Extractions;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Interfaces.Repositories;

public interface IExtractedSearcherRepository 
    : IRepository<ExtractedSearch, long>
{
    Task<ExtractedSearch?> FindByStoreAndQueryAsync(string store, string query, CancellationToken cancellationToken = default);
    Task SaveExtractedAsync(string store, string query, string rawJson, CancellationToken cancellationToken = default);
}