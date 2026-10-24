using PriceComparator.Application.UseCases.MetricProduct.Get;
using PriceComparator.Application.UseCases.SearchProduct.Get;

namespace PriceComparator.Application.UseCases.SearchResult.Get;

public sealed record SearchResultsResponse(
    SearchResponse Search,
    MetricResponse Metrics
);