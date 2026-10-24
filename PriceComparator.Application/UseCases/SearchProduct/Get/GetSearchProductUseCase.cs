
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.Services;


namespace PriceComparator.Application.UseCases.SearchProduct.Get;

public sealed class GetSearchProductUseCase(
    IEnumerable<IProductOfferSearcher> searchers,
    IConceptRepository conceptRepository,
    IRankedService rankedService,
    IProductService productService)
    : IScopedDependency
{
    private readonly IReadOnlyCollection<IProductOfferSearcher> _searchers = searchers.ToArray();

    public async Task<SearchResponse> ExecuteAsync(ProductRequest request, CancellationToken cancellationToken = default)
    {
        var query = request.Query?.Trim();
        var normalizedQuery = NormalizerService.Normalize(query);
        var concepts = await conceptRepository.GetAllAsync(cancellationToken);
        var productsByStore = new Dictionary<string, IReadOnlyCollection<Product>>(StringComparer.OrdinalIgnoreCase);

        foreach (var searcher in _searchers)
        {
            try
            {
                var processedProducts = await searcher.GetProcessedProductsAsync(query!, cancellationToken);

                if (processedProducts is not null)
                {
                    productsByStore[searcher.StoreCode] = processedProducts;
                    Console.WriteLine( $"Se usa cache existente | StoreCode={searcher.StoreCode}");
                    continue;
                }

                // 1. Obtener ofertas desde el supermercado.
                var offers = await searcher.SearchAsync(query!, cancellationToken);
                
                // 2. Eliminar ofertas duplicadas.
                var uniqueOffers = productService.RemoveDuplicates(offers);
                var products = productService.PrepareProducts(query!, normalizedQuery, uniqueOffers, concepts);
                
                // 3. Calcular la relevancia de los productos.
                products = rankedService.Rank(query!, products);
                 
                // 4. Guardar los productos por supermercado.
                await searcher.SaveProducts(products, query!, cancellationToken);
                productsByStore[searcher.StoreCode] = products;
            }
            catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Error ejecutando {searcher.StoreCode}: " + $"{exception.GetType().Name} - " + $"{exception.Message}");
                productsByStore[searcher.StoreCode] = [];
            }
        }

        var totalResults = productsByStore.Values.Sum(products => products.Count);
 
        return new SearchResponse(Query: query!, TotalResults: totalResults, Stores: productsByStore);
    }
    
}