namespace PriceComparator.Domain.Entities;

public sealed class ProductSearchData
{
    public string StoreCode { get; init; } = string.Empty;
    public string Query { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; }
    public IReadOnlyCollection<ProductOffer> Products { get; init; } = [];
}