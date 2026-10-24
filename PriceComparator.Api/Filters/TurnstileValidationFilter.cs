using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PriceComparator.Application.Interfaces.Services;

namespace PriceComparator.Api.Filters;

public sealed class TurnstileValidationFilter(ITurnstileVerificationService verificationService) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var token = context.HttpContext.Request.Headers["X-Turnstile-Token"].ToString();

        if (string.IsNullOrWhiteSpace(token))
        {
            context.Result = CreateForbiddenResult("Debe completar la verificación de seguridad.");
            return;
        }

        var isValid = await verificationService.VerifyAsync(token, context.HttpContext.RequestAborted);

        if (!isValid)
        {
            context.Result = CreateForbiddenResult("La verificación de seguridad es inválida o ha expirado.");
            return;
        }

        await next();
    }

    private static ObjectResult CreateForbiddenResult(string detail)
    {
        return new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Verificación de seguridad fallida",
            Detail = detail
        })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}