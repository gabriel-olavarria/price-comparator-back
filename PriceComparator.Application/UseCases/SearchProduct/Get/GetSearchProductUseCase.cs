using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;



namespace PriceComparator.Application.UseCases.SearchProduct.Get;

public sealed class GetSearchProductUseCase(
    IEnumerable<IProductOfferSearcher> searchers,
    IConceptRepository conceptRepository,
    IRankedService rankedService,
    IProductService productService,
    IPackagingRepository  packagingRepository)
    : IScopedDependency
{
    private readonly IReadOnlyCollection<IProductOfferSearcher> _searchers = searchers.ToArray();

    public async Task<SearchResponse> ExecuteAsync(ProductRequest request, CancellationToken cancellationToken = default)
    {
        var queryTrim = request.Query.Trim();
        var concepts = await conceptRepository.GetAllConceptsAsync(cancellationToken);
        var packagings = await packagingRepository.GetAllWithTermsAsync(cancellationToken);
        var productsByStore = new Dictionary<string, IReadOnlyCollection<Product>>(StringComparer.OrdinalIgnoreCase);

        foreach (var searcher in _searchers)
        {
            try
            {
                // 1. Obtener ofertas desde el supermercado.
                var (offers, fromCache) = await searcher.SearchAsync(queryTrim, cancellationToken);

                // Si ya están procesados, devolver directamente.
                if (fromCache)
                {
                    productsByStore[searcher.StoreCode] = offers;
                    Console.WriteLine($"[{searcher.StoreCode}] Se devuelve productos procesados.");

                    continue;
                }
                
                // 2. Eliminar ofertas duplicadas.
                var uniqueOffers = productService.RemoveDuplicates(offers);
               
                // 3. Preparación de productos
                var products =  productService.PrepareProducts(queryTrim, uniqueOffers, concepts, packagings);
                
                // 4. Calcular la relevancia de los productos.
                products = rankedService.Rank(queryTrim, products);
                
                // 5. Guardar los productos por supermercado.
                await searcher.SaveProcessed(products, queryTrim, cancellationToken);
                
                productsByStore[searcher.StoreCode] = products.OrderBy(p => p.Price).ToList();
                Console.WriteLine($"Finalizó {searcher.StoreCode}");

            }
            catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Error ejecutando {searcher.StoreCode}: {exception}");
                productsByStore[searcher.StoreCode] = [];
            }
        }

        var totalResults = productsByStore.Values.Sum(products => products.Count);
 
        return new SearchResponse(Query: queryTrim, TotalResults: totalResults, Stores: productsByStore);
    }
    
}