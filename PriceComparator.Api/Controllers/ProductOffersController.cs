using Microsoft.AspNetCore.Mvc;
using PriceComparator.Application.UseCases.ProductOffers.SearchProductOffers;

namespace PriceComparator.Api.Controllers;

[ApiController]
[Route("api/product-offers")]
public sealed class ProductOffersController : ControllerBase
{
    private readonly ISearchProductOffersUseCase _searchProductOffersUseCase;

    public ProductOffersController(ISearchProductOffersUseCase searchProductOffersUseCase)
    {
        _searchProductOffersUseCase = searchProductOffersUseCase;
    }

    [HttpGet("search")]
    public async Task<ActionResult<SearchProductOffersResponse>> SearchAsync(
        [FromQuery] string query,
        [FromQuery] string[]? categories,
        CancellationToken cancellationToken)
    {
        var request = new SearchProductOffersRequest(
            Query: query,
            Categories: categories ?? []);

        var response = await _searchProductOffersUseCase.ExecuteAsync(
            request,
            cancellationToken);

        return Ok(response);
    }
}