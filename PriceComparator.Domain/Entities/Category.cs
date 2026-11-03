using PriceComparator.Domain.Common;

namespace PriceComparator.Domain.Entities;

public sealed class Category : Entity<long>
{
    public string Name { get; set; } = string.Empty;

    public List<Concept> Concepts { get; set; } = [];
}