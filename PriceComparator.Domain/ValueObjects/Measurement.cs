using PriceComparator.Domain.Enums;

namespace PriceComparator.Domain.ValueObjects;

public sealed record Measurement(
    UnitType? UnitType,
    int Quantity,
    double? QuantityUnit,
    Unit? Unit
);