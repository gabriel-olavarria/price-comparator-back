using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PriceComparator.Api.Security;
using PriceComparator.Application.Interfaces.Services;

namespace PriceComparator.Api.Filters;

public sealed class TurnstileValidationFilter(
    ITurnstileVerificationService verificationService,
    MobileClientKeyValidator mobileClientKeyValidator,
    IWebHostEnvironment environment,
    ILogger<TurnstileValidationFilter> logger
    ) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;

        if (mobileClientKeyValidator.HasProvidedKey(request))
        {
            if (mobileClientKeyValidator.IsValid(request))
            {
                await next();
                return;
            }

            logger.LogWarning("Clave de la app mobile rechazada.");
            context.Result = CreateForbiddenResult("La clave de la aplicación es inválida.");
            return;
        }

        // La web local no envía clave ni token de Turnstile.
        if (environment.IsDevelopment())
        {
            await next();
            return;
        }

        var token = request.Headers["X-Turnstile-Token"].ToString();

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