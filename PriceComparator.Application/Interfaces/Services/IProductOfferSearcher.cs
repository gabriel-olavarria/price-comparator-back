using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Interfaces.Services;

public interface IProductOfferSearcher
{
    string StoreCode { get; }

    Task<IReadOnlyCollection<Product>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task SaveProducts(IReadOnlyCollection<Product> products, string query, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Product>?> GetProcessedProductsAsync(string query, CancellationToken cancellationToken = default);
}