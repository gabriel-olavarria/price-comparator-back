using PriceComparator.Application.DTOs;

namespace PriceComparator.Application.Interfaces.Services;

public interface IConsumoVehicularService
{
    Task<ConsumoVehicularResponseDto> GetCatalogAsync(CancellationToken cancellationToken);
    
    Task<VehicleResponseDto> GetVehiclesAsync(UserVehicleRequest userVehicleRequest, CancellationToken cancellationToken);
}