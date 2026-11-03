using PriceComparator.Application.DTOs.Extractions;

namespace PriceComparator.Application.DTOs.Extractions;

public sealed record RawExtractionDto(
    string Store,
    string Query,
    string RawJson,
    int ProductCount,
    DateTimeOffset ExtractedAt
);