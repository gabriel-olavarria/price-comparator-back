using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Interfaces.Services;

public interface IProductOfferSearcher
{
    string StoreCode { get; }

    Task<(IReadOnlyCollection<Product> Products, bool FromCache)> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task SaveExtracted(string json, string normalizedQuery, CancellationToken cancellationToken);
    Task SaveProcessed(IReadOnlyCollection<Product> products, string query, CancellationToken cancellationToken = default);

}