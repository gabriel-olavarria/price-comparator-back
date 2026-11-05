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

        if (classification is null)
            return product;

        var measurement = classification.Measurement;

        string canoName = string.Empty;
        string canKey = string.Empty;

        if (!string.IsNullOrWhiteSpace(classification.PackagingName))
        {
            var packagingName = classification.PackagingName;
            var packagingTerm = classification.PackagingTermName;

            if (string.Equals(
                    packagingName,
                    packagingTerm,
                    StringComparison.OrdinalIgnoreCase))
            {
                canoName = $"{packagingTerm} ";
                canKey = $"{packagingTerm}_";
            }
            else
            {
                canoName = $"{packagingName} {packagingTerm}".Trim() + " ";
                canKey = $"{packagingName.ToLowerInvariant()}_{packagingTerm}".TrimEnd('_') + "_";
            }
        }

        string? quantity = measurement is null
            ? string.Empty
            : measurement.Quantity > 1
                ? measurement.QuantityUnitValue.ToString()
                : measurement.DisplayQuantity.ToString();

        string contentKey = !string.IsNullOrWhiteSpace(measurement?.DisplayContentName)
            ? $"{measurement.DisplayContentName}_{measurement.Quantity}_"
            : string.Empty;

        string contentName = !string.IsNullOrWhiteSpace(measurement?.DisplayContentName)
            ? $"{measurement.DisplayContentName} {measurement.Quantity} "
            : string.Empty;

        product.CanonicalKey =
            $"{classification.Concept}_" +
            contentKey +
            $"{product.Brand?.Replace(" ", "")}_" +
            canKey +
            $"{classification.SubtypeValue}_" +
            $"{quantity}{measurement?.DisplayUnit}";

        product.CanonicalName =
            $"{classification.Concept} " +
            contentName +
            $"{product.Brand} " +
            canoName +
            $"{classification.SubtypeName} " +
            $"{quantity} " +
            $"{measurement?.DisplayUnit}";

        return product;
    }

    public IReadOnlyCollection<CanonicalGroupModel> GroupByCanonicalKey(
        IReadOnlyDictionary<string, IReadOnlyCollection<Product>> stores)
    {
        return stores
            .SelectMany(store => store.Value)
            .Where(product => !string.IsNullOrWhiteSpace(product.CanonicalKey))
            .GroupBy(product => product.CanonicalKey!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CanonicalGroupModel(
                CanonicalKey: group.Key,
                CanonicalName: group.Select(product => product.CanonicalName)
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)),
                Products: group.ToList()
            )).ToList();
    }

    public IReadOnlyCollection<CanonicalGroupModel> GetCheapestByStore(IEnumerable<CanonicalGroupModel> groups)
    {
        return groups
            .Select(group => new CanonicalGroupModel(
                CanonicalKey: group.CanonicalKey,
                CanonicalName: group.CanonicalName,
                Products: group.Products
                    .GroupBy(product => product.Store.Code, StringComparer.OrdinalIgnoreCase)
                    .Select(storeGroup => storeGroup.MinBy(product => product.Price)!)
                    .OrderBy(product => product.Price)
                    .ToArray()
            )).ToArray();
    }

    public IReadOnlyCollection<CanonicalGroupModel> Top10CheapestGroup(
        IReadOnlyCollection<CanonicalGroupModel> cheapestStore)
    {
        return cheapestStore
            .Where(group => group.Products.Count > 0)
            .OrderBy(group => group.Products.Min(product => product.Price))
            .Take(10)
            .ToArray();
    }

    public IReadOnlyCollection<CheapestStoreTotalModel> GetCheapestStoreTotals(IEnumerable<CanonicalGroupModel> groups)
    {
        return groups
            .Where(group => group.Products.Any())
            .Select(group => new
            {
                group.CanonicalKey,
                group.CanonicalName,
                Product = group.Products.MinBy(product => product.Price)!,
            })
            .GroupBy(
                item => item.Product.Store.Code,
                StringComparer.OrdinalIgnoreCase
            )
            .Select(storeGroup => new CheapestStoreTotalModel(
                Store: storeGroup.Key,
                SellerName: storeGroup
                    .Select(item => item.Product.Store.Name)
                    .FirstOrDefault() ?? string.Empty,
                TotalCanonicalEconomic: storeGroup
                    .Select(item => item.CanonicalKey)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                Canonical: storeGroup
                    .Select(item => new CheapestCanonicalProduct(
                        CanonicalKey: item.CanonicalKey,
                        CanonicalName: item.CanonicalName,
                        Product: item.Product
                    )).ToArray()))
            .OrderByDescending(store => store.TotalCanonicalEconomic)
            .ToArray();
    }
}