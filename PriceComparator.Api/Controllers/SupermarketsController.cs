using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PriceComparator.Api.Configurations;
using PriceComparator.Api.Filters;
using PriceComparator.Application.DTOs;
using PriceComparator.Application.UseCases.NearbySupermarket.Get;

namespace PriceComparator.Api.Controllers;

[ApiController]
[Route("api/supermarkets")]
 
public class SupermarketsController(
    GetNearbySupermarketsUseCase getNearbySupermarkets
) : ControllerBase
{
    [EnableRateLimiting(RateLimitingConfiguration.ProductSearch)]
    [HttpPost("nearby")]
    public Task<NearbySupermarketsResponse> GetByNearby([FromBody] NearbySupermarketsRequest request, CancellationToken cancellationToken)
    {
        return getNearbySupermarkets.ExecuteAsync(request, cancellationToken);
    }
}