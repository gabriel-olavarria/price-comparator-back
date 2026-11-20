using PriceComparator.Application.DTOs;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;

namespace PriceComparator.Application.UseCases.FuelStation.Get;

public sealed class GetCatalogVehiculeUseCase(
    IConsumoVehicularService consumoVehicularService
) : IScopedDependency
{
    public async Task<ConsumoVehicularResponseDto> ExecuteAsync(CancellationToken cancellationToken)
    {
   

        cancellationToken.ThrowIfCancellationRequested();

        // 1. Obtener token de autenticación CNE
        var response = await consumoVehicularService.GetCatalogAsync(cancellationToken);
        
        
        return response;
    }
}
