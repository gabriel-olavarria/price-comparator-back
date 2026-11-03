namespace PriceComparator.Application.DTOs.Extractions;

public sealed record ExtractedTodayDto(
    string Store,
    string Query,
    int ProductCount,
    DateTime ExtractedAt,
    string RawJson
);