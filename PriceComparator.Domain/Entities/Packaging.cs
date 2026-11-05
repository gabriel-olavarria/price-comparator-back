using PriceComparator.Domain.Common;

namespace PriceComparator.Domain.Entities;

public sealed class Packaging : Entity<long>
{
    public string Name { get; set; } = string.Empty;

    public List<PackagingTerm> Terms { get; set; } = [];
}