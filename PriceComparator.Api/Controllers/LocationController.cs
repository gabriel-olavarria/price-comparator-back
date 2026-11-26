using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PriceComparator.Api.Configurations;
using PriceComparator.Application.Interfaces.Services;

[ApiController]
[Route("api/location")]
public sealed class LocationController(
    IGeoapifyService geoapifyService) : ControllerBase
{
    [EnableRateLimiting(RateLimitingConfiguration.ProductSearch)]
    [HttpGet("autocomplete")]
    public async Task<IActionResult> Autocomplete([FromQuery] string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 3)
        {
            return BadRequest("Ingresa al menos tres caracteres.");
        }

        var results = await geoapifyService.AutocompleteAddressAsync(text, cancellationToken);

        return Ok(results);
    }
}