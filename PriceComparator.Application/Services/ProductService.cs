
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Application.UseCases.MetricProduct.Get;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.Services;
using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Application.Services;

public class ProductService(
    IConceptService conceptService,
    CanonicalService canonicalService
) : IProductService, ISingletonDependency
{
    public IReadOnlyCollection<Product> PrepareProducts(
        string query,
        string normalizedQuery,
        IEnumerable<Product> products,
        IReadOnlyCollection<Concept> concepts)
    {
        var queryConcept = conceptService.DetectFromText(query, concepts);

        return products
            .Select(product => Prepare(
                normalizedQuery,
                product,
                queryConcept,
                concepts))
            .ToArray();
    }

    private Product Prepare(
        string normalizedQuery,
        Product product,
        Concept? queryConcept,
        IReadOnlyCollection<Concept> concepts)
    {
        var normalizedName = NormalizerService.Normalize(product.Name);
        var normalizedBrand = NormalizerService.Normalize(product.Brand);

        product.Normalized = new Normalized
        {
            Query = normalizedQuery,
            Name = normalizedName,
            Brand = normalizedBrand
        };

        // Detectar el concepto propio del producto.
        var productConcept = conceptService.DetectFromText(
            product.Name,
            concepts);

        // Solo utilizar el concepto de la búsqueda como respaldo
        // cuando no se identifica uno en el nombre del producto.
        var concept = productConcept ?? queryConcept;

        if (concept is not null)
        {
            DetectClasification(product, concept);
        }

        return canonicalService.GenerateCanonicalKey(product);
    }

    public IReadOnlyCollection<Product> RemoveDuplicates(
        IEnumerable<Product> products)
    {
        return products
            .DistinctBy(
                product => product.ProductUrl.AbsoluteUri,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
    
    private static Product DetectClasification(
        Product product,
        Concept concept)
    {
        var measurement = MeasurementService.Extract(
            product.Normalized!.Name);

        var subtypes = concept.Subtypes
            .Select(s => new PriceComparator.Domain.ValueObjects.Subtype
            {
                Id = s.Name,
                Terms = s.Terms
                    .Select(t => t.Name)
                    .ToList()
            })
            .ToList();

        var subtype = SubTypeService.Extract(
            product.Normalized.Name,
            subtypes);

        product.Clasification = new Clasification
        {
            Id = concept.Id.ToString(),
            Category = concept.Category?.Name,
            Subtype = subtype,
            Measurement = measurement
        };

        return product;
    }

    public IReadOnlyList<StoreProductTotal> GetTotalProductsByStore(
        IReadOnlyDictionary<string, IReadOnlyCollection<Product>> stores)
    {
        return stores
            .Select(store => new StoreProductTotal(
                TotalProducts: store.Value.Count,
                Store: store.Key))
            .OrderByDescending(x => x.TotalProducts)
            .ToArray();
    }

    public IReadOnlyList<CanonicalProductTotal> GetTotalCanonicalKeysByStore(
        IReadOnlyDictionary<string, IReadOnlyCollection<Product>> stores)
    {
        return stores
            .Select(store => new CanonicalProductTotal(
                TotalCanonicalKey: store.Value
                    .Where(product =>
                        !string.IsNullOrWhiteSpace(product.CanonicalKey))
                    .Select(product => product.CanonicalKey!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                Store: store.Key))
            .OrderByDescending(x => x.TotalCanonicalKey)
            .ToArray();
    }
}
