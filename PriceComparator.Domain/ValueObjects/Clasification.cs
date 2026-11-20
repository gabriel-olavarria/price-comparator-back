using PriceComparator.Domain.Enums;

namespace PriceComparator.Domain.ValueObjects;

public sealed record Classification
{
    public string Category { get; set; } = string.Empty;
    public string Concept { get; set; } = string.Empty;
    public string? SubtypeName { get; set; } = string.Empty;
    public string? SubtypeValue { get; set; } = string.Empty;
    public string? PackagingName { get; set; } = string.Empty;
    public string? PackagingTermName { get; set; } = string.Empty;
    public Measurement? Measurement { get; set; }
}