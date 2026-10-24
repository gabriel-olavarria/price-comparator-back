using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Application.Models;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Services;

public class CanonicalService : ICanonicalService, ISingletonDependency 
{
    public Product GenerateCanonicalKey(Product product)
    {
        var classification = product.Clasification;

        if (classification?.Subtype is null)
        {
            product.CanonicalKey = null;
            return product;
        }

        product.CanonicalKey =
            $"{classification.Category}_" +
            $"{classification.Id}_" +
            $"{classification.Subtype}_" +
            $"{classification.Measurement?.QuantityUnit}" +
            $"{classification.Measurement?.Unit}";

        return product;
    }
    
    public IReadOnlyCollection<CanonicalGroupModel> GroupByCanonicalKey(IReadOnlyDictionary<string, IReadOnlyCollection<Product>> stores)
    {
        return stores
            .SelectMany(store => store.Value)
            .Where(product => !string.IsNullOrWhiteSpace(product.CanonicalKey))
            .GroupBy(product => product.CanonicalKey!, StringComparer.OrdinalIgnoreCase
            )
            .Select(group => new CanonicalGroupModel(CanonicalKey: group.Key, Products: group.ToList()
            )).ToList();
    }
    
    public IReadOnlyCollection<CanonicalGroupModel> GetCheapestByStore(IEnumerable<CanonicalGroupModel> groups)
    {
        return groups
            .Select(group => new CanonicalGroupModel(
                CanonicalKey: group.CanonicalKey,
                Products: group.Products
                    .GroupBy(product => product.Store.Code, StringComparer.OrdinalIgnoreCase)
                    .Select(storeGroup => storeGroup.MinBy(product => product.Price)!)
                    .OrderBy(product => product.Price)
                    .ToArray()
            )).ToArray();
    }
    
    public IReadOnlyCollection<CanonicalGroupModel> Top10CheapestGroup(IReadOnlyCollection<CanonicalGroupModel> cheapestStore)
    {
        return cheapestStore
            .Where(group => group.Products.Count > 0)
            .OrderBy(group => group.Products.Min(product => product.Price))
            .Take(10)
            .ToArray();
    }
}


