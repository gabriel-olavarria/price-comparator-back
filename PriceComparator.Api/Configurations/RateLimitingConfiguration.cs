using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace PriceComparator.Api.Configurations;

public static class RateLimitingConfiguration
{
    public const string ProductSearch = "product-search";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(ProductSearch, context =>
            {
                var clientIp = context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: clientIp,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }
}