
using PriceComparator.Domain.Common;

namespace PriceComparator.Domain.Entities;

public class Rules : Entity<long>
{
    // Relaciones opcionales
    public long? ConceptId { get; set; }
    public long? ConceptTermId { get; set; }
    public long? PackagingId { get; set; }
    public long? PackagingTermId { get; set; }
    public long? SubTypeId { get; set; }
    public long? SubTypeTermId { get; set; }

    // Condición
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    // Configuración
    public int Priority { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    // Navegaciones
    public Concept? Concept { get; set; }
    public ConceptTerm? ConceptTerm { get; set; }
    public Packaging? Packaging { get; set; }
    public PackagingTerm? PackagingTerm { get; set; }
    public Subtype? Subtype { get; set; }
    public SubtypeTerm? SubTypeTerm { get; set; }
}