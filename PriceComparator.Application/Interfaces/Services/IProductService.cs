using PriceComparator.Application.UseCases.MetricProduct.Get;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Application.Interfaces.Services;

public interface IProductService
{
    Task<IReadOnlyCollection<Product>>  PrepareProductsAsync(string query, string normalizedQuery, IEnumerable<Product> products, IReadOnlyCollection<Concept> concepts, IReadOnlyCollection<Packaging> packagings);
    IReadOnlyCollection<Product> RemoveDuplicates(IEnumerable<Product> products);
    IReadOnlyList<StoreProductTotal> GetTotalProductsByStore(IReadOnlyDictionary<string, IReadOnlyCollection<Product>> stores);
    IReadOnlyList<CanonicalProductTotal> GetTotalCanonicalKeysByStore(IReadOnlyDictionary<string, IReadOnlyCollection<Product>> stores);
}