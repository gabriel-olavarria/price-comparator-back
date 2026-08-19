using PriceComparator.Application.Interfaces.ProductOffers;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.UseCases.ProductOffers.SearchProductOffers;

public sealed class SearchProductOffersUseCase : ISearchProductOffersUseCase
{
    private readonly IReadOnlyCollection<IProductOfferSearcher> _searchers;

    public SearchProductOffersUseCase(IEnumerable<IProductOfferSearcher> searchers)
    {
        ArgumentNullException.ThrowIfNull(searchers);

        _searchers = searchers.ToArray();
    }

    public async Task<SearchProductOffersResponse> ExecuteAsync(SearchProductOffersRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Console.WriteLine($"Buscadores registrados: {_searchers.Count}");

        var query = request.Query.Trim();

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

        var productsByStore = new Dictionary<string, IReadOnlyCollection<ProductOfferResult>>(StringComparer.OrdinalIgnoreCase);

        foreach (var searcher in _searchers)
        {
            try
            {
                Console.WriteLine($"Ejecutando buscador: {searcher.GetType().Name}");

                var offers = await searcher.SearchAsync(searchTerm, cancellationToken);

                Console.WriteLine($"Resultados obtenidos antes de filtrar: {offers.Count}");

                var filteredOffers = FilterOffers(offers, query, categories);

                var products = filteredOffers
                    .GroupBy(offer => offer.ProductUrl)
                    .Select(group => group.First())
                    .Select(MapToResult)
                    .OrderBy(product => product.Price)
                    .ToArray();

                productsByStore[searcher.StoreCode] = products;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Error ejecutando {searcher.StoreCode}: {exception.Message}");

                productsByStore[searcher.StoreCode] = [];
            }
        }

        var totalResults = productsByStore.Values.Sum(products => products.Count);

        return new SearchProductOffersResponse(
            Query: query,
            TotalResults: totalResults,
            Stores: productsByStore);
    }

    private static IReadOnlyCollection<ProductOffer> FilterOffers(
        IReadOnlyCollection<ProductOffer> offers,
        string query,
        IReadOnlyCollection<string> categories)
    {
        IEnumerable<ProductOffer> filteredOffers = offers;

        if (!string.IsNullOrWhiteSpace(query))
        {
            filteredOffers = filteredOffers.Where(offer =>
                offer.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (categories.Count > 0)
        {
            filteredOffers = filteredOffers.Where(offer =>
                categories.Any(selectedCategory =>
                    offer.Categories.Any(productCategory =>
                        productCategory.Contains(selectedCategory, StringComparison.OrdinalIgnoreCase) ||
                        selectedCategory.Contains(productCategory, StringComparison.OrdinalIgnoreCase))));
        }

        return filteredOffers.ToArray();
    }

    private static ProductOfferResult MapToResult(ProductOffer offer)
    {
        return new ProductOfferResult(
            Name: offer.Name,
            Price: offer.Price,
            Categories: offer.Categories,
            ProductUrl: offer.ProductUrl.ToString(),
            ImageUrl: offer.ImageUrl?.ToString(),
            Brand: offer.Brand,
            SellerName: offer.SellerName,
            IsMarketplace: !string.Equals(offer.SellerType, "INTERNAL", StringComparison.OrdinalIgnoreCase));
    }
}