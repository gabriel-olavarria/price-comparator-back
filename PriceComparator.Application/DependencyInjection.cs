using Microsoft.Extensions.DependencyInjection;
using PriceComparator.Application.Interfaces.DependencyInjection;

namespace PriceComparator.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.Scan(scan => scan
            .FromAssemblies(typeof(DependencyInjection).Assembly)

            .AddClasses(classes => classes.AssignableTo<IScopedDependency>())
            .AsSelfWithInterfaces()
            .WithScopedLifetime()

            .AddClasses(classes => classes.AssignableTo<ITransientDependency>())
            .AsSelfWithInterfaces()
            .WithTransientLifetime()

            .AddClasses(classes => classes.AssignableTo<ISingletonDependency>())
            .AsSelfWithInterfaces()
            .WithSingletonLifetime());

        return services;
    }
}