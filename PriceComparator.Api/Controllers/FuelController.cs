using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PriceComparator.Api.Configurations;
using PriceComparator.Application.DTOs;
using PriceComparator.Application.UseCases.FuelStation.Get;

namespace PriceComparator.Api.Controllers;

[ApiController]
[Route("api/fuels")]
 
public class FuelStationsController(
    GetFuelByLocationUseCase getFuelByLocationUseCase,
    GetCatalogVehiculeUseCase getCatalogVehiculeUseCase,
    GetConsumeVehicleUseCase getConsumeVehicleUseCase
) : ControllerBase
{
    
    [EnableRateLimiting(RateLimitingConfiguration.ProductSearch)]
    [HttpGet("catalog")]
    public Task<ConsumoVehicularResponseDto>  GetCatalogVehicule(CancellationToken cancellationToken)
    {
        return getCatalogVehiculeUseCase.ExecuteAsync(cancellationToken);
    }

    [EnableRateLimiting(RateLimitingConfiguration.ProductSearch)]
    [HttpGet("characteristics")]
    public Task<VehicleResponseDto> GetConsumeVehicle([FromQuery] UserVehicleRequest request, CancellationToken cancellationToken)
    {
        return getConsumeVehicleUseCase.ExecuteAsync(request, cancellationToken);
    }
    
    [EnableRateLimiting(RateLimitingConfiguration.ProductSearch)]
    [HttpPost("calculate")]
    public Task<GetFuelByLocationResponse> Calculate([FromBody] GetFuelByLocationRequest request, CancellationToken cancellationToken)
    {
        return getFuelByLocationUseCase.ExecuteAsync(request, cancellationToken);
    }
    
    
}