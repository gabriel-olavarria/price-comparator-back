using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.UseCases.ProductOffers.SearchProductOffers;

public interface ISearchProductOffersUseCase
{
    Task<SearchProductOffersResponse> ExecuteAsync(
        SearchProductOffersRequest request,
        CancellationToken cancellationToken = default);
}