using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Interfaces.ProductOffers;

public interface IProductOfferSearcher
{
    string StoreCode { get; }

    Task<IReadOnlyCollection<ProductOffer>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}