using PriceComparator.Api.ExceptionHandlers;

namespace PriceComparator.Api.Configurations;

public static class ExceptionHandlingConfiguration
{
    public static IServiceCollection AddApiExceptionHandling(
        this IServiceCollection services)
    {
        services.AddExceptionHandler<ApiExceptionHandler>();

        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var problem = context.ProblemDetails;

                problem.Instance ??=
                    context.HttpContext.Request.Path;

                problem.Extensions["traceId"] =
                    context.HttpContext.TraceIdentifier;

                if (problem.Status == StatusCodes.Status500InternalServerError)
                {
                    problem.Title = "Error interno del servidor";
                    problem.Detail = "No se pudo procesar la solicitud.";
                }
            };
        });

        return services;
    }
}