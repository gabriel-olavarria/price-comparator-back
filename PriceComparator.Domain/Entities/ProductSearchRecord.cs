using PriceComparator.Domain.Common;

namespace PriceComparator.Domain.Entities;

public sealed class ProductSearchRecord : Entity<long>
{
    public string Name { get; set; } = string.Empty;
    public string Store { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string CanonicalKey { get; set; } = string.Empty;
    public string CanonicalName { get; set; } = string.Empty;
    public DateTime SearchDate { get; set; }
}