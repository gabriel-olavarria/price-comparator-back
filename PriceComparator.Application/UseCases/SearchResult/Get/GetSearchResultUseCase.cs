using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.UseCases.MetricProduct.Get;
using PriceComparator.Application.UseCases.SearchProduct.Get;

namespace PriceComparator.Application.UseCases.SearchResult.Get;

public sealed class GetSearchResultsUseCase(
    GetSearchProductUseCase getSearchProduct,
    GetMetricProductUseCase getMetricProduct
) : IScopedDependency
{
    public async Task<SearchResultsResponse> ExecuteAsync(ProductRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Buscar productos en los supermercados
        var productResponse = await getSearchProduct.ExecuteAsync(
            request,
            cancellationToken
        );

        // 2. Calcular las métricas con los productos encontrados
        var metricResponse = getMetricProduct.Execute(
            new MetricRequest(productResponse.Stores)
        );

        // 3. Devolver búsqueda y métricas
        return new SearchResultsResponse(
            Search: productResponse,
            Metrics: metricResponse
        );
    }
}