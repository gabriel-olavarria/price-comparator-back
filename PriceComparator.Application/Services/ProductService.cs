using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.Services;
using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Application.Services;

public class ProductService(
    IConceptService conceptService, 
    ICanonicalService canonicalService
    ) 
    : IProductService, ISingletonDependency
{
    public IReadOnlyCollection<Product> PrepareProducts(string query, string normalizedQuery, IEnumerable<Product> products, IReadOnlyCollection<Concept> concepts)
    {
        return [.. products.Select(product => Prepare(query, normalizedQuery, product, concepts))];
    }

    private Product Prepare(string query, string normalizedQuery, Product product, IReadOnlyCollection<Concept> concepts)
    {
        var normalizedName = NormalizerService.Normalize(product.Name);
        var normalizedBrand = NormalizerService.Normalize(product.Brand);
        product.Normalized = new Normalized
        {
            Query = normalizedQuery,
            Name = normalizedName,
            Brand = normalizedBrand,
        };
        
        
        var queryConcept = conceptService.DetectFromText(query, concepts);
        var productClasification = DetectClasification(product, queryConcept!);
        return canonicalService.GenerateCanonicalKey(productClasification);
    }
    
    public IReadOnlyCollection<Product> RemoveDuplicates(IEnumerable<Product> products)
    {
        return products.GroupBy(product => product.ProductUrl)
            .Select(group => group.First())
            .ToArray();
    }
    
    private Product DetectClasification(Product product, Concept queryConcept)
    {
        var measurement = MeasurementService.Extract(product.Normalized!.Name);
        var subtype = SubTypeService.Extract(product.Normalized.Name, queryConcept.Subtypes);

        product.Clasification = new Clasification
        {
            Id = queryConcept.Id,
            Category = queryConcept.Category,
            Subtype = subtype,
            Measurement = measurement
        };

         
        return product;
    }

     
}

 