using PriceComparator.Application.Models;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Application.Interfaces.Services;

public interface ICanonicalService
{
    Product GenerateCanonicalKey(Product product);
    IReadOnlyCollection<CanonicalGroupModel> GroupByCanonicalKey(IEnumerable<Product> products);
    IReadOnlyCollection<CanonicalGroupModel> GetCheapestByStore(IEnumerable<CanonicalGroupModel> groups);
}