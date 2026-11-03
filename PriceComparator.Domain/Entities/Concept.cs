using PriceComparator.Domain.Common;

namespace PriceComparator.Domain.Entities;

public sealed class Concept : Entity<long>
{
    public string Name { get; set; } = string.Empty;

    public long? CategoryId { get; set; }

    public Category? Category { get; set; }

    public List<Aliases> Aliases { get; set; } = [];

    public List<Subtype> Subtypes { get; set; } = [];
}