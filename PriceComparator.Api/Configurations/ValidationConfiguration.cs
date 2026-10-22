using Microsoft.AspNetCore.Mvc;

namespace PriceComparator.Api.Configurations;

public static class ValidationConfiguration
{
    public static IServiceCollection AddApiValidation(
        this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var problem = new ValidationProblemDetails(
                    context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Error de validación",
                    Instance = context.HttpContext.Request.Path
                };

                problem.Extensions["traceId"] =
                    context.HttpContext.TraceIdentifier;

                return new BadRequestObjectResult(problem);
            };
        });

        return services;
    }
}