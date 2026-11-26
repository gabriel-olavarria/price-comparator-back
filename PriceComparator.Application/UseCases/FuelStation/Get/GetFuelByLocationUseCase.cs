using PriceComparator.Application.DTOs;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;

namespace PriceComparator.Application.UseCases.FuelStation.Get;

public sealed class GetFuelByLocationUseCase(
    IFuelStationService fuelStationService,
    ICneAuthService  cneAuthService,
    IConsumoVehicularService consumoVehicularService
) : IScopedDependency
{
    public async Task<GetFuelByLocationResponse> ExecuteAsync(FuelCalculationRequest request, CancellationToken cancellationToken)
    {
        // 1. Obtener token de autenticación CNE
        var token = await cneAuthService.GetToken(cancellationToken);
        
        // 2. Obtener listado de estaciones de combustible
        List<CneFuelStationDto> stations = await fuelStationService.GetListFuels(token, cancellationToken, request.UserLocation.Comuna, request.UserLocation.Region);
        
        // 3. Identificar las estaciones más cercanas al domicilio distinguidas por marca
        var nearestStations = fuelStationService.GetNearestFuelStationsByBrand(stations, request.UserLocation.Latitud, request.UserLocation.Longitud);

        // 4. Calcular gasto estimado de combustible para cada estación 
        foreach (var combustible in nearestStations.SelectMany(station => station.Combustibles.Values))
        {
            if (combustible.PrecioLitro is { } precio)
            {
                combustible.PrecioDistancia = fuelStationService.CalculateFuelCost(request.Tramo.TotalDistanceKm, (decimal)precio, request.RendimientoMixto);
            }
        }

        // 5. Calcular el consumo estimado de gasolina
        var consumeComsumtion = fuelStationService.CalculateFuelConsumption(request.Tramo.TotalDistanceKm, request.RendimientoMixto);
        
        // 6. Construir respuesta final
        return new GetFuelByLocationResponse
        {
            Station = nearestStations.FirstOrDefault(),
            NearestStations = nearestStations,
            TotalDistanceRouteKm = request.Tramo.TotalDistanceKm,
            ConsumptionValue = consumeComsumtion.ConsumptionValue,
            ConsumptionUnit = consumeComsumtion.ConsumptionUnit
        };
    }
}
