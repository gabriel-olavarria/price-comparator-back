using PriceComparator.Application.DTOs;

namespace PriceComparator.Application.Interfaces.Services;

public interface IFuelStationService
{
    Task<List<CneFuelStationDto>> GetListFuels(string token, CancellationToken cancellationToken);
    Task<List<CneFuelStationDto>> GetListFuelsByLocation(List<CneFuelStationDto> fuelstation, string region, string comuna, CancellationToken cancellationToken);
    NearestFuelStationDto? GetNearestFuelStation(List<CneFuelStationDto> stations, double latitude, double longitude);
    Task<RouteDto> CalculateRouteAsync(double homeLatitude, double homeLongitude, IReadOnlyList<FeatureResultDto> supermarkets, CancellationToken cancellationToken);
    FuelCostDto CalculateFuelCost(double distanceKm, decimal fuelPrice, decimal fuelEfficiencyKmPerLiter);
}