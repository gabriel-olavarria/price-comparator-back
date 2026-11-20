using PriceComparator.Domain.Common;

namespace PriceComparator.Domain.Entities;

public sealed class SubtypeTerm : Entity<long>
{
    public string Name { get; set; } = string.Empty;

    public long SubtypeId { get; set; }

    public Subtype Subtype { get; set; } = null!;
    
    public List<Rules> Rules { get; set; } = [];
}