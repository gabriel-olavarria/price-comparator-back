namespace PriceComparator.Domain.Models;

public sealed record ProductScoreDebug(
    string? QueryConcept,
    string? ProductConcept,
    bool ExactName,
    bool StartsWithQuery,
    bool ContainsQuery,
    bool CategoryMatch,
    bool BrandMatch,
    double TokenCoverage,
    IReadOnlyCollection<string> Reasons);