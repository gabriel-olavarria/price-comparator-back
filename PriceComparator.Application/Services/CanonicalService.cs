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
    
    public IReadOnlyCollection<CanonicalGroupModel> GroupByCanonicalKey(IEnumerable<Product> products)
    {
        return
        [
            .. products
                .Where(product => !string.IsNullOrWhiteSpace(product.CanonicalKey))
                .GroupBy(product => product.CanonicalKey!)
                .Select(group => new CanonicalGroupModel(CanonicalKey: group.Key, Products: group.ToArray()))
        ];
    }
    
    public IReadOnlyCollection<CanonicalGroupModel> GetCheapestByStore(IEnumerable<CanonicalGroupModel> groups)
    {
        return groups.Select(group => new CanonicalGroupModel(CanonicalKey: group.CanonicalKey, Products: group.Products
                    .GroupBy(product => product.Store.Code)
                    .Select(storeGroup => storeGroup.MinBy(product => product.Price)!)
                    .OrderBy(product => product.Price)
                    .ToArray()))
            .ToArray();
    }
}


