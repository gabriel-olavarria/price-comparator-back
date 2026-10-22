using PriceComparator.Domain.Entities;
using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Application.Interfaces.Services;

public interface IProductService
{
    IReadOnlyCollection<Product> PrepareProducts(string query, IEnumerable<Product> products, IReadOnlyCollection<Concept> concepts);
    IReadOnlyCollection<Product> RemoveDuplicates(IEnumerable<Product> products);
 
}