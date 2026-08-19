namespace PriceComparator.Application.UseCases.ProductOffers.SearchProductOffers;

public sealed record SearchProductOffersResponse(
    string? Query,
    int TotalResults,
    IReadOnlyDictionary< string, IReadOnlyCollection<ProductOfferResult>> Stores);