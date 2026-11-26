using PriceComparator.Application.DTOs;

namespace PriceComparator.Application.Interfaces.Services;

public interface IFuelStationService
{
    Task<List<CneFuelStationDto>> GetListFuels(string token, CancellationToken cancellationToken, string comuna, string region);
    List<CneFuelStationDto> GetNearestFuelStationsByBrand(List<CneFuelStationDto> stations, double latitude, double longitude);
    Task<RouteDto> CalculateRouteAsync(double homeLatitude, double homeLongitude, IReadOnlyList<FeatureResultDto> supermarkets, CancellationToken cancellationToken);
    decimal  CalculateFuelCost(double distanceKm, decimal fuelPrice, decimal fuelEfficiencyKmPerLiter);
    public FuelConsumptionDto CalculateFuelConsumption(double distanceKm, decimal fuelEfficiencyKmPerLiter);

}