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
        
        // 3. Identificar estación más cercana al domicilio
        var nearestStation = fuelStationService.GetNearestFuelStation(stations, request.UserLocation.Latitud, request.UserLocation.Longitud);

        // 4. Calcular gasto estimado de combustible
        var combustibles = nearestStation.Combustibles;

        foreach (var combustible in combustibles)
        {
            if (combustible.Value.PrecioLitro is null)
            {
                continue;
            }
            decimal precio = (decimal)combustible.Value.PrecioLitro;
            combustible.Value.PrecioDistancia = fuelStationService.CalculateFuelCost(request.Tramo.TotalDistanceKm, precio, request.RendimientoMixto);

        }
        
        // 5. Calcular el consumo estimado de gasolina
        var consumeComsumtion = fuelStationService.CalculateFuelConsumption(request.Tramo.TotalDistanceKm, request.RendimientoMixto);
        
        // 6. Construir respuesta final
        return new GetFuelByLocationResponse
        {
            Station = nearestStation,
            TotalDistanceRouteKm = request.Tramo.TotalDistanceKm,
            ConsumptionValue = consumeComsumtion.ConsumptionValue,
            ConsumptionUnit = consumeComsumtion.ConsumptionUnit
        };
    }
}
