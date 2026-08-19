namespace PriceComparator.Application.UseCases.ProductOffers.SearchProductOffers;

public sealed record SearchProductOffersRequest(string? Query, IReadOnlyCollection<string> Categories);