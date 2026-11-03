using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.DTOs.Extractions;


public sealed record ProcessedTodayDto(
    string Store,
    string Query,
    int ProductCount,
    DateTime ProcessedAt,
    IReadOnlyCollection<Product> Products
);