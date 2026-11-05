
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
    IProductSearchRecordRepository  productSearchRecordRepository,
    IPackagingRepository  packagingRepository
) : IProductService, IScopedDependency
{
    public async Task<IReadOnlyCollection<Product>> PrepareProductsAsync(string query, string normalizedQuery, IEnumerable<Product> products, IReadOnlyCollection<Concept> concepts, IReadOnlyCollection<Packaging> packagings)
    {
        var results = new List<Product>();
        var records = new List<ProductSearchRecord>();
        foreach (var product in products)
        {
            var result = await PrepareAsync(normalizedQuery, product, concepts, packagings);
            ProductSearchRecord productSearch = new()
            {
                Name = result.Name,
                Store = result.Store.Name,
                Query = normalizedQuery,
                CanonicalKey = result.CanonicalKey ?? "",
                CanonicalName = result.CanonicalName ?? "",
                SearchDate = DateTime.UtcNow
            };

         //   await productSearchRecordRepository.AddAsync(productSearch);
            
            results.Add(result);
        }

        return results;
    }

    private async Task<Product> PrepareAsync(string normalizedQuery, Product product, IReadOnlyCollection<Concept> concepts, IReadOnlyCollection<Packaging> packagings)
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
        var productConcept = conceptService.DetectFromText(product.Name, concepts);

        if (productConcept is not null)
        {
            await DetectClasificationAsync(product, productConcept, packagings);
        }

        return canonicalService.GenerateCanonicalKey(product);
    }

    public IReadOnlyCollection<Product> RemoveDuplicates(IEnumerable<Product> products)
    {
        return products.DistinctBy(product => product.ProductUrl.AbsoluteUri, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    
    private async Task<Product> DetectClasificationAsync(Product product, Concept concept, IReadOnlyCollection<Packaging> packagings)
    {
        //Detectar Caracteristicas
        Packaging? packaging = packagingRepository.Extract(product.Name, packagings);
        var measurement = MeasurementService.Extract(product.Normalized!.Name);
        
        //Detectar Sybtipo
        var subtypes = concept.Subtypes;
        Subtype? subtype = subtypeRepository.Extract(product.Normalized.Name, subtypes);
        
        if (subtype is null)
        { 
            subtype = subtypeRepository.ExtractByName(product, concept, measurement, packaging);
            //await subtypeRepository.AddAsync(subtype);
        }
        
        product.Clasification = new Clasification
        {
            Concept = concept.Name,
            Category = concept.Category!.Name,
            SubtypeName = subtype.Name,
            SubtypeValue = subtype.Value,
            Measurement = measurement,
            PackagingName = packaging?.Name,
            PackagingTermName = packaging?.Terms[0]?.Name
        };

        return product;
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
