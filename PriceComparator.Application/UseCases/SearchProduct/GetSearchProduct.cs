
using PriceComparator.Application.Interfaces;
using PriceComparator.Application.UseCases.Ranking;
using PriceComparator.Domain.Common;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.UseCases.SearchProduct;

public sealed class GetSearchProduct :  IScopedDependency
{
    private readonly IReadOnlyCollection<IProductOfferSearcher> _searchers;
    private readonly IConceptRepository _conceptRepository;
    private readonly ProductRelevanceRanker _ranker;

    public GetSearchProduct(IEnumerable<IProductOfferSearcher> searchers, IConceptRepository conceptRepository, ProductRelevanceRanker ranker)
    {
        ArgumentNullException.ThrowIfNull(searchers);
        ArgumentNullException.ThrowIfNull(conceptRepository);
        ArgumentNullException.ThrowIfNull(ranker);

        _searchers = searchers.ToArray();
        _conceptRepository = conceptRepository;
        _ranker = ranker;
    }

    public async Task<Result<ProductResponse>> ExecuteAsync(ProductRequest request, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("===== SEARCHERS INYECTADOS =====");
        Console.WriteLine($"TOTAL: {_searchers.Count}");

        foreach (var searcher in _searchers)
        {
            Console.WriteLine($"{searcher.GetType().FullName} | StoreCode={searcher.StoreCode}");
        }

        Console.WriteLine("================================");
        ArgumentNullException.ThrowIfNull(request);
        var query = request.Query?.Trim();
        var categories = request.Categories
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Select(category => category.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (string.IsNullOrWhiteSpace(query) && categories.Length == 0)
        {
            throw new ArgumentException("Debe indicar un nombre de producto o al menos una categoría.");
        }

        var searchTerm = !string.IsNullOrWhiteSpace(query) ? query : categories[0];

        /*
         * Los conceptos se cargan una sola vez para toda
         * la ejecución del caso de uso.
         * Actualmente, vienen desde product-concepts.json.
         * En el futuro el repositorio podrá obtenerlos desde
         * PostgreSQL sin modificar este caso de uso.
         */
        var concepts = await _conceptRepository.GetAllAsync(cancellationToken);
        var productsByStore = new Dictionary<string, IReadOnlyCollection<Product>>(StringComparer.OrdinalIgnoreCase);

        foreach (var searcher in _searchers)
        {
            try
            {
                /*
                 * 1. Obtener ofertas desde el supermercado.
                 */
                var offers = await searcher.SearchAsync(searchTerm, cancellationToken);

                /*
                 * 2. Aplicar el filtro de categorías
                 * solicitado por el usuario.
                 */
                var filteredOffers = FilterOffers(offers, categories);

                /*
                 * 3. Eliminar ofertas duplicadas antes
                 * de ejecutar el ranking.
                 */
                var uniqueOffers = filteredOffers.GroupBy(offer => offer.ProductUrl).Select(group => group.First()).ToArray();

                /*
                 * 4. Calcular la relevancia de cada producto.
                 * El Ranker:
                 * - identifica el concepto buscado;
                 * - identifica el concepto del producto;
                 * - calcula coincidencias;
                 * - elimina resultados bajo el score mínimo;
                 * - ordena por score descendente.
                 */
                var rankedOffers = _ranker.Rank(searchTerm, uniqueOffers, concepts);

                /*
                 * 5. Convertir los productos ya ordenados
                 * al modelo de respuesta.
                 * No hacemos un nuevo OrderBy aquí porque
                 * perderíamos el orden generado por el Ranker.
                 */
                var products = rankedOffers.Select(ranked => ranked.Product).ToArray();
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

        return new ProductResponse(
            Query: query!,
            TotalResults: totalResults,
            Stores: productsByStore);
    }

    private static IReadOnlyCollection<Product> FilterOffers(IReadOnlyCollection<Product> offers, IReadOnlyCollection<string> categories) {
        if (categories.Count == 0)
        {
            return offers;
        }
        return offers.Where(offer => categories.Any(selectedCategory => offer.Categories.Any(productCategory => productCategory.Contains(selectedCategory, StringComparison.OrdinalIgnoreCase) || selectedCategory.Contains(productCategory, StringComparison.OrdinalIgnoreCase)))).ToArray();
    }
}