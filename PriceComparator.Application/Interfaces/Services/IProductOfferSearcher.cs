using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Interfaces.Services;

public interface IProductOfferSearcher
{
    string StoreCode { get; }

    Task<IReadOnlyCollection<Product>> SearchAsync(string query, CancellationToken cancellationToken = default);
}