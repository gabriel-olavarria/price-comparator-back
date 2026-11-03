using PriceComparator.Domain.Common;

namespace PriceComparator.Domain.Entities;

public sealed class Subtype : Entity<long>
{
    public string Name { get; set; } = string.Empty;

    public long ConceptId { get; set; }

    public Concept Concept { get; set; } = null!;

    public List<Term> Terms { get; set; } = [];
}