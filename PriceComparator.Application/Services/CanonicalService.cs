using System.Text.RegularExpressions;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Application.Models;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.Enums;

namespace PriceComparator.Application.Services;

public class CanonicalService : ICanonicalService, ISingletonDependency
{
    public Product GenerateCanonicalKey(Product product)
    {
        var classification = product.Classification;

        if (classification is null)
            return product;

        var pack = GetPack(product);
        var packaging = GetPackaging(classification.PackagingName, classification.PackagingTermName);

 

        var query = product.Normalized?.Query;
        var brand = product.Normalized?.Brand;

        var isEqualQuery =
            !string.IsNullOrWhiteSpace(query) &&
            !string.IsNullOrWhiteSpace(brand) &&
            string.Equals(
                query.Replace(" ", ""),
                brand.Replace(" ", ""),
                StringComparison.OrdinalIgnoreCase
            );
        
        string subtype = isEqualQuery ? string.Empty : product.Classification?.SubtypeName ?? string.Empty;
       
        product.CanonicalName = BuildCanonicalName(
            classification.Concept,
            pack,
            product.Brand,
            packaging,
            classification.Measurement?.DisplayUnit,
            subtype
        );
        
        product.CanonicalKey = BuildCanonicalKey(
            classification.Concept,
            pack,
            product.Brand,
            packaging,
            classification.Measurement?.DisplayCanKey,
            subtype
            );

        return product;
    }

    public IReadOnlyCollection<CanonicalGroupModel> GroupByCanonicalKey(IReadOnlyDictionary<string, IReadOnlyCollection<Product>> stores)
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

    public IReadOnlyCollection<CanonicalGroupModel> Top10CheapestGroup(IReadOnlyCollection<CanonicalGroupModel> cheapestStore)
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

    /// <summary>
    /// Obtiene la descripción del pack según la cantidad de unidades detectadas.
    /// </summary>
    /// <param name="product">Producto con su clasificación y medida.</param>
    /// <returns>Descripción del pack o null si no corresponde.</returns>
    private static string? GetPack(Product product)
    {
        var measurement = product.Classification?.Measurement;

        if (measurement?.Type != UnitType.Units)
            return null;

        return $"Pack {measurement.Quantity}";
    }
    /// <summary>
    /// Obtiene los tipos de envase, eliminando valores vacíos y duplicados.
    /// </summary>
    private static string[] GetPackaging(string? packagingName, string? packagingTermName)
    {
        return new[] { packagingName, packagingTermName }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
    /// <summary>
    /// Construye el nombre canónico legible del producto.
    /// </summary>
    private static string BuildCanonicalName(string? concept, string? pack, string? brand, string[] packaging, string? measurement, string? subtype)
    {
        return JoinParts(" ",
            new[] { concept, pack, brand!.Replace("-", " "), subtype }
                .Concat(packaging)
                .Append(measurement)
        );
    }
    /// <summary>
    /// Construye la clave canónica normalizada del producto.
    /// </summary>
    private static string BuildCanonicalKey(string? concept, string? pack, string? brand, string[] packaging, string? measurement, string? subtype)
    {
        var brandKey = brand?.Replace(" ", "").Replace("-", "");

        var packKey = pack?.Replace(" ", "_");
        
        var subtypeKey = subtype?.Replace(" ", "_");

        var parts = new[] { concept, packKey, subtypeKey }
            .Concat(packaging)
            .Select(x => x?.ToLowerInvariant())
            .Append(measurement);

        return JoinParts("_", parts);
    }
    /// <summary>
    /// Une los componentes mediante un separador, omitiendo valores
    /// nulos, vacíos o compuestos únicamente por espacios en blanco.
    /// </summary>
    /// <param name="separator">Separador entre los componentes.</param>
    /// <param name="values">Componentes que se unirán.</param>
    /// <returns>Cadena con los componentes válidos, sin espacios al inicio ni al final de cada uno.</returns>
    private static string JoinParts(string separator, IEnumerable<string?> values)
    {
        return string.Join(
            separator,
            values.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
        );
    }
}