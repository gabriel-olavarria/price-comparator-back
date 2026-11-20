using PriceComparator.Application.UseCases.MetricProduct.Get;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Interfaces.Services;

public interface IProductService
{
    IReadOnlyCollection<Product>  PrepareProducts(string queryTrim, IEnumerable<Product> products, IReadOnlyCollection<Concept> concepts, IReadOnlyCollection<Packaging> packagings);
    IReadOnlyCollection<Product> RemoveDuplicates(IEnumerable<Product> products);
    IReadOnlyList<StoreProductTotal> GetTotalProductsByStore(IReadOnlyDictionary<string, IReadOnlyCollection<Product>> stores);
    IReadOnlyList<CanonicalProductTotal> GetTotalCanonicalKeysByStore(IReadOnlyDictionary<string, IReadOnlyCollection<Product>> stores);
}