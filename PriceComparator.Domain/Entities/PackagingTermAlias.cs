using PriceComparator.Domain.Common;

namespace PriceComparator.Domain.Entities;

public class PackagingTermAlias: Entity<long>
{
    public long PackagingTermId { get; set; }

    public string Name { get; set; } = string.Empty;

    public PackagingTerm PackagingTerm { get; set; } = null!;
}