
namespace PriceComparator.Application.DTOs;

public sealed class GetFuelByLocationResponse
{
    public CneFuelStationDto? Station { get; init; }
    public List<CneFuelStationDto> NearestStations { get; init; } = [];
    public double TotalDistanceRouteKm { get; init; }
    public decimal ConsumptionValue { get; init; }
    public string ConsumptionUnit { get; init; } = string.Empty;
    
}

