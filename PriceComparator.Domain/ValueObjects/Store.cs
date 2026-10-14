namespace PriceComparator.Domain.ValueObjects;

public sealed record Store
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}