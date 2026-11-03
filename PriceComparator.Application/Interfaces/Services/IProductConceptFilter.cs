using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Interfaces.Services;

public interface IProductConceptFilter
{
    IReadOnlyCollection<Product> Filter(
        IEnumerable<Product> products,
        string query);

    bool IsMatch(Product product, string query);
}