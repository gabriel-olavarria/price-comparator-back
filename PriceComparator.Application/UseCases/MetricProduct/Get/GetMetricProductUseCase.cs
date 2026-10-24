using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.UseCases.MetricProduct.Get;

public sealed class GetMetricProductUseCase(
    IProductService productService,
    ICanonicalService canonicalService
) : IScopedDependency
{
    public MetricResponse Execute(MetricRequest request)
    {
        // Total de productos por supermercado
        var totalProductsByStore = productService.GetTotalProductsByStore(request.Stores);
        
        // Total de claves canónicas distintas por supermercado
        var totalCanonicalKeysByStore = productService.GetTotalCanonicalKeysByStore(request.Stores);
        
        // Agrupación canónica
        var canonicalGroups = canonicalService.GroupByCanonicalKey(request.Stores);
        
        // Agrupación canónica con un producto por supermercado
        var cheapestByStoreGroups = canonicalService.GetCheapestByStore(canonicalGroups);
        
        // Top 10 de agrupación canónica 
        var top10Groups =  canonicalService.Top10CheapestGroup(cheapestByStoreGroups);

        // Producto más barato de los 10 grupos
        var cheapestProduct = top10Groups.SelectMany(group => group.Products).MinBy(product => product.Price);
        
        // Construir respuesta
        return new MetricResponse(
            TotalProductsByStore: totalProductsByStore,
            TotalProductsByCanonicalKey: totalCanonicalKeysByStore,
            EconomicProduct: cheapestProduct is null ? null : MapEconomicProduct(cheapestProduct),
            TopCanonicalGroups: top10Groups
                .Select(group => new CanonicalGroupResponse(
                        CanonicalKey: group.CanonicalKey, 
                        Products: 
                        group.Products.Select(MapEconomicProduct).ToArray()
                ))
                .ToArray()
        );
    }
    
    private static EconomicProduct MapEconomicProduct(Product product)
    {
        return new EconomicProduct(
            Name: product.Name,
            Price: product.Price,
            Store: product.Store.Name,
            ProductUrl: product.ProductUrl,
            ImageUrl: product.ImageUrl,
            CanonicalKey: product.CanonicalKey
        );
    }
}