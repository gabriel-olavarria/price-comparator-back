namespace PriceComparator.Api.Configurations;

public static class SecurityHeadersConfiguration
{
    public static IApplicationBuilder UseApiSecurityHeaders(
        this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.TryAdd(
                    "X-Content-Type-Options",
                    "nosniff");

                return Task.CompletedTask;
            });

            await next(context);
        });
    }
}