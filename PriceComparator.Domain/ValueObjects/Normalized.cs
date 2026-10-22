namespace PriceComparator.Domain.ValueObjects;

public sealed record Normalized
{
    public string Query { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
}