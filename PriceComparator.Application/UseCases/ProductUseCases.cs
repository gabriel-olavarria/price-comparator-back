using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.UseCases.MetricProduct.Get;
using PriceComparator.Application.UseCases.SearchProduct.Get;
using PriceComparator.Application.UseCases.SearchResult.Get;

namespace PriceComparator.Application.UseCases;

public sealed class ProductUseCases(
    GetSearchProductUseCase getSearchProduct,
    GetMetricProductUseCase getMetricProduct,
    GetSearchResultsUseCase getSearchResults)
    : IScopedDependency
{
    public GetSearchProductUseCase GetSearchProduct { get; } = getSearchProduct;
    public GetMetricProductUseCase GetMetricProduct { get; } = getMetricProduct;
    public GetSearchResultsUseCase GetSearchResults { get; } = getSearchResults;
}