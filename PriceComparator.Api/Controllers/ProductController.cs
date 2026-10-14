using Microsoft.AspNetCore.Mvc;
using PriceComparator.Application.UseCases;
using PriceComparator.Application.UseCases.SearchProduct;
using PriceComparator.Domain.Common;

namespace PriceComparator.Api.Controllers;

[ApiController]
[Route("api/product")]
public class ProductController(ProductUseCases product)
{
    [HttpGet("search")]
    public async Task<IActionResult> SearchAsync([FromQuery] string query, [FromQuery] string[]? categories, CancellationToken cancellationToken)
    {
        var request = new ProductRequest(Query: query, Categories: categories ?? []);
        return await product.GetSearchProduct.ExecuteAsync(request, cancellationToken).ToValueOrProblemDetails();;
    }
}