
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Application.UseCases.MetricProduct.Get;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.Services;
using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Application.Services;

public class ProductService(
    IConceptService conceptService,
    CanonicalService canonicalService,
    ISubTypeRepository subtypeRepository,
    IPackagingRepository  packagingRepository
) : IProductService, IScopedDependency
{
    public IReadOnlyCollection<Product> PrepareProducts(string queryTrim, IEnumerable<Product> products, IReadOnlyCollection<Concept> concepts, IReadOnlyCollection<Packaging> packagings)
    {
        var results = new List<Product>();
        foreach (var product in products)
        {
            var result =  PrepareAsync(
                queryTrim,
                product,
                concepts,
                packagings
            );

            if (result is null)
                continue;

            results.Add(result);
        }

        return results;
    }

    private Product PrepareAsync(string queryTrim, Product product, IReadOnlyCollection<Concept> concepts, IReadOnlyCollection<Packaging> packagings)
    {
        Product productTrasnform = product;
        
        // 1. Detectar el concepto propio del producto.
        Concept? concept = conceptService.DetectFromText(productTrasnform.Name, concepts);

        //Validacion si no existe concepto en el producto
        if (concept is null)
         return productTrasnform;
        
        //Validacion para que el nombre comience con el valor del concepto
        if (!product.Name.TrimStart().StartsWith(concept.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            return productTrasnform;

        // Validar la palabra inmediatamente después del concepto
        var remaining = product.Name[concept.Name.Length..].TrimStart();
        if (remaining.Equals("con", StringComparison.OrdinalIgnoreCase) ||
            remaining.Equals("y", StringComparison.OrdinalIgnoreCase) ||
            remaining.StartsWith("con ", StringComparison.OrdinalIgnoreCase) ||
            remaining.StartsWith("y ", StringComparison.OrdinalIgnoreCase))
        {
            return productTrasnform;
        }

        // 2. Normalizacion
        productTrasnform = NormalizeProduct(queryTrim, concept, productTrasnform);
        
        // 3. Clasificacion 
        productTrasnform = DetectClassificationAsync(productTrasnform, concept, packagings);
        
        // 4. Canonizacion 
        productTrasnform = canonicalService.GenerateCanonicalKey(productTrasnform);
        
 
        return productTrasnform;
    }

    private Product NormalizeProduct(string queryTrim, Concept concept,  Product product)
    {
        var normalizedName = NormalizerService.Normalize(product.Name);
        var normalizedBrand = NormalizerService.Normalize(product.Brand);
        
        product.Normalized = new Normalized
        {
            Query = queryTrim,
            Name = normalizedName,
            Brand = normalizedBrand,
        };

        return product;
    }

    private Product DetectClassificationAsync(Product product, Concept concept, IReadOnlyCollection<Packaging> packagings)
    {
        var subtypes = concept.Subtypes;
        
        //Detectar Caracteristicas
        Packaging? packaging = packagingRepository.Extract(product.Name, packagings, product.Slug);
        
        //Detectar Medidas
        Measurement? measurement = MeasurementService.Extract(product.Name);
        
        //Detectar Sybtipo
        Subtype subtype = subtypeRepository.Extract(product, subtypes, concept, measurement, packaging);
        
        product.Classification = new Classification
        {
            Concept = concept.Name,
            Category = concept.Category!.Name,
            SubtypeName = subtype.Name,
            SubtypeValue = subtype.Value,
            Measurement = measurement,
            PackagingName = packaging?.Name,
            PackagingTermName = packaging?.Terms[0].Name
        };

        return product;
    }
    
    public IReadOnlyCollection<Product> RemoveDuplicates(IEnumerable<Product> products)
    {
        return products.DistinctBy(product => product.ProductUrl.AbsoluteUri, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    
    public IReadOnlyList<StoreProductTotal> GetTotalProductsByStore(IReadOnlyDictionary<string, IReadOnlyCollection<Product>> stores)
    {
        return stores
            .Select(store => new StoreProductTotal(TotalProducts: store.Value.Count, Store: store.Key))
            .OrderByDescending(x => x.TotalProducts)
            .ToArray();
    }

    public IReadOnlyList<CanonicalProductTotal> GetTotalCanonicalKeysByStore(IReadOnlyDictionary<string, IReadOnlyCollection<Product>> stores)
    {
        return stores
            .Select(store => new CanonicalProductTotal(TotalCanonicalKey: store.Value
                    .Where(product => !string.IsNullOrWhiteSpace(product.CanonicalKey))
                    .Select(product => product.CanonicalKey!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                Store: store.Key))
            .OrderByDescending(x => x.TotalCanonicalKey)
            .ToArray();
    }
}
