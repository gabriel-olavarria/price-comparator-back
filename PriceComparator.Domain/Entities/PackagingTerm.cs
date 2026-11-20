using PriceComparator.Domain.Common;

namespace PriceComparator.Domain.Entities;

public sealed class PackagingTerm : Entity<long>
{
    public string Name { get; set; } = string.Empty;

    public long PackagingId { get; set; }

    public Packaging Packaging { get; set; } = null!;
    
    public ICollection<PackagingTermAlias> Aliases { get; set; } = new List<PackagingTermAlias>();
    
    public List<Rules> Rules { get; set; } = [];
}