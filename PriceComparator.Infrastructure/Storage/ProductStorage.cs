using PriceComparator.Domain.Entities;

namespace PriceComparator.Infrastructure.Storage;


public sealed class ProductStorage
{
    public string StoreCode { get; init; } = string.Empty;

    public string Query { get; init; } = string.Empty;

    public DateTime GeneratedAt { get; init; }

    public IReadOnlyCollection<ProductOffer> Products { get; init; } = [];
}