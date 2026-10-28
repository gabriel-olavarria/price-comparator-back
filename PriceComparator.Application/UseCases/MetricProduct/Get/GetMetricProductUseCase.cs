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
        
        //Supermercados y la cantidad de canonicals en las que su producto es el mas economico
        var cheapestCanonicalStore=  canonicalService.GetCheapestStoreTotals(cheapestByStoreGroups);
        var cheapestStoresResponse = MapCheapestStoresResponse(cheapestCanonicalStore);
        
        // Construir respuesta
        return new MetricResponse(
            TotalProductsByStore: totalProductsByStore,
            TotalProductsByCanonicalKey: totalCanonicalKeysByStore,
            EconomicProduct: cheapestProduct is null ? null : MapEconomicProduct(cheapestProduct),
            TopCanonicalGroups: top10Groups
                .Select(group => new CanonicalGroupResponse(
                        CanonicalKey: group.CanonicalKey, 
                        CanonicalName: group.CanonicalName,
                        Products: group.Products.Select(MapEconomicProduct).ToArray()
                )).ToArray(),
            CheapestCanonicalStore : cheapestStoresResponse
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
            CanonicalKey: product.CanonicalKey,
            CanonicalName: product.CanonicalName
        );
    }
    
    private static IReadOnlyList<CheapestStoreResponse> MapCheapestStoresResponse(IReadOnlyCollection<CheapestStoreTotalModel> cheapestStores)
    {
        return cheapestStores
            .Select(store => new CheapestStoreResponse(
                Store: store.Store,
                SellerName: store.SellerName,
                TotalCanonicalEconomic: store.TotalCanonicalEconomic,
                Canonical: store.Canonical
                    .Select(item => new CheapestCanonicalResponse(
                        CanonicalKey: item.CanonicalKey,
                        Product: new EconomicProduct(
                            Name: item.Product.Name,
                            Price: item.Product.Price,
                            Store: item.Product.Store.Code,
                            ProductUrl: item.Product.ProductUrl,
                            ImageUrl: item.Product.ImageUrl,
                            CanonicalKey: item.Product.CanonicalKey,
                            CanonicalName: item.Product.CanonicalName
                        ))).ToArray()
            )).ToArray();
    }
}