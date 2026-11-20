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
    public async Task<GetFuelByLocationResponse> ExecuteAsync(GetFuelByLocationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        // 1. Obtener token de autenticación CNE
        var token = await cneAuthService.GetToken(cancellationToken);
        
        // 2. Obtener listado de estaciones de combustible
        List<CneFuelStationDto> stations = await fuelStationService.GetListFuels(token, cancellationToken);

        // 3. Filtrar estaciones por región y comuna del usuario
        List<CneFuelStationDto> stationsByLocation = await fuelStationService.GetListFuelsByLocation(stations, request.Region, request.Comuna, cancellationToken);

        // 4. Identificar estación más cercana al domicilio
        var nearestStation = fuelStationService.GetNearestFuelStation(stationsByLocation, request.Latitud, request.Longitud);

        // 5. Calcula recorrido total por tramos sobre la ruta
        var route = await fuelStationService.CalculateRouteAsync(request.Latitud, request.Longitud, request.Supermercados, cancellationToken);
        
        //6. Obtener caracteristicas del vehiculo del usuario para obtener el redimiento
       // var vehicleResponse = await consumoVehicularService.GetVehiclesAsync(request.UserVehicleRequest!, cancellationToken);
       // var vehicle = vehicleResponse.Items.FirstOrDefault();
        //decimal? efficiencyKmPerLiter = vehicle?.RendimientoMixto;
        
        // 7. Calcular gasto estimado de combustible
        FuelCostDto? fuelCost = null;

        if (nearestStation is not null && request.RendimientoMixto is > 0)
        {
            var selectedFuelType = request.TipoCombustible
                .Trim()
                .ToUpperInvariant();

            decimal? pricePerLiter = selectedFuelType switch
            {
                "93" => nearestStation.Precios.Gasolina93?.Precio,
                "95" => nearestStation.Precios.Gasolina95?.Precio,
                "97" => nearestStation.Precios.Gasolina97?.Precio,
                "DI" or "DIESEL" => nearestStation.Precios.Diesel?.Precio,
                _ => null
            };

            if (pricePerLiter is > 0 && request.RendimientoMixto is > 0)
            {
                fuelCost = fuelStationService.CalculateFuelCost(route.TotalDistanceKm, pricePerLiter.Value, request.RendimientoMixto);
       
            }
        }

        // 8. Construir respuesta final
        return new GetFuelByLocationResponse
        {
            EstacionCercana = nearestStation,
            Recorrido = route,
            GastoEstimado = fuelCost
        };
    }
}
