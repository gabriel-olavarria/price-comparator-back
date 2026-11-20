using PriceComparator.Application.DTOs;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;

namespace PriceComparator.Application.UseCases.FuelStation.Get;

public sealed class GetConsumeVehicleUseCase(
    IConsumoVehicularService consumoVehicularService
) : IScopedDependency
{
    public async Task<VehicleResponseDto> ExecuteAsync(UserVehicleRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Obtener caracteristicas del vehiculo
        var vehicleResponse = await consumoVehicularService.GetVehiclesAsync(request, cancellationToken);

        
        
        return vehicleResponse;
    }
}