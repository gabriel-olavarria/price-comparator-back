using PriceComparator.Domain.Enums;

namespace PriceComparator.Domain.ValueObjects;

public sealed record Measurement(
    UnitType? UnitType,
    int Quantity,
    double? QuantityUnitValue,
    Unit? QuantityUnit,
    double? DisplayQuantity = null,
    Unit? DisplayUnit = null,
    string? DisplayUnitName = null,
    string? DisplayName = null,
    string? SecondDisplayName = null,
    string? DisplayContentName = null
);