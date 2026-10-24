using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PriceComparator.Api.Configurations;
using PriceComparator.Api.Filters;
using PriceComparator.Application.UseCases;
using PriceComparator.Application.UseCases.SearchProduct.Get;
using PriceComparator.Application.UseCases.SearchResult.Get;

namespace PriceComparator.Api.Controllers;

[ApiController]
[Route("api/product")]
[TypeFilter(typeof(TurnstileValidationFilter))]
public class ProductController(ProductUseCases product) : ControllerBase
{
    [EnableRateLimiting(RateLimitingConfiguration.ProductSearch)]
    [HttpGet("search")]
    public Task<SearchResultsResponse> SearchAsync([FromQuery] ProductRequest request, CancellationToken cancellationToken)
    {
        return product.GetSearchResults.ExecuteAsync(request, cancellationToken);
    }

}