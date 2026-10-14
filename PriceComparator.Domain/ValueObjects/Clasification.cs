using PriceComparator.Domain.Enums;

namespace PriceComparator.Domain.ValueObjects;

public sealed record Clasification
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Subtype { get; set; } = string.Empty;
    public Measurement? Measurement { get; set; }
}