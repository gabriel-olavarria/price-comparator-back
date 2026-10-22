namespace PriceComparator.Application.Models;

public sealed record ProductScoreDebug(
    string? ProductConcept,
    bool ExactName,
    bool StartsWithQuery,
    bool ContainsQuery,
    bool CategoryMatch,
    bool BrandMatch,
    double TokenCoverage,
    IReadOnlyCollection<string> Reasons);