namespace PriceComparator.Domain.ValueObjects;

public sealed record Subtype
{
    public string Id { get; set; } = string.Empty;
    public IReadOnlyList<string> Terms { get; set; } = [];
}