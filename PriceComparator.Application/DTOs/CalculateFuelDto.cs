
namespace PriceComparator.Application.DTOs;

public sealed class NearestFuelStationDto
{
    public CneFuelStationDto Station { get; init; } = new();

    public double DistanceKm { get; init; }

    public FuelPricesDto Precios { get; init; } = new();
}

public sealed class FuelCostDto
{
    public double DistanceKm { get; init; }

    public decimal FuelEfficiencyKmPerLiter { get; init; }

    public decimal FuelPrice { get; init; }

    public decimal EstimatedLiters { get; init; }

    public decimal EstimatedCost { get; init; }
}
