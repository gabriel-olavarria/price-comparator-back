using Microsoft.Extensions.DependencyInjection;
using PriceComparator.Application.Interfaces;
using PriceComparator.Infrastructure.ProductOffers.Jumbo;
using PriceComparator.Infrastructure.ProductOffers.SantaIsabel;
using PriceComparator.Infrastructure.ProductOffers.Tottus;
using PriceComparator.Infrastructure.Repositories;

namespace PriceComparator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        RegisterDependencies(services);
        RegisterHttpClients(services);
        return services;
    }

    private static void RegisterDependencies(IServiceCollection services)
    {
        Console.WriteLine();
        Console.WriteLine("===== PRODUCT CONCEPT REPOSITORY =====");

        var implementsSingleton = typeof(ISingletonDependency).IsAssignableFrom(typeof(ConceptRepository));
        var implementsRepository = typeof(IConceptRepository).IsAssignableFrom(typeof(ConceptRepository));

        Console.WriteLine($"Implementa ISingletonDependency: {implementsSingleton}");
        Console.WriteLine($"Implementa IProductConceptRepository: {implementsRepository}");
        Console.WriteLine($"Assembly ProductConceptRepository: " + $"{typeof(ConceptRepository).Assembly.FullName}");
        Console.WriteLine($"Assembly Infrastructure DI: " + $"{typeof(DependencyInjection).Assembly.FullName}");
        Console.WriteLine("======================================");
        Console.WriteLine();

        /*
         * Registro automático de dependencias
         * pertenecientes a Infrastructure.
         */
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

        /*
         * Verificamos qué ocurrió DESPUÉS del Scan.
         */
        Console.WriteLine();
        Console.WriteLine("===== REGISTRO IConceptRepository =====");

        var registrations = services
            .Where(service => service.ServiceType == typeof(IConceptRepository))
            .ToArray();

        Console.WriteLine($"TOTAL REGISTRADOS: {registrations.Length}");

        foreach (var registration in registrations)
        {
            Console.WriteLine($"ServiceType: " + $"{registration.ServiceType.FullName}");
            Console.WriteLine($"ImplementationType: " + $"{registration.ImplementationType?.FullName ?? "null"}");
            Console.WriteLine($"ImplementationFactory: " + $"{(registration.ImplementationFactory is null ? "NO" : "SI")}");
            Console.WriteLine($"Lifetime: {registration.Lifetime}");
            Console.WriteLine("---");
        }

        Console.WriteLine(registrations.Length > 0 ? "IProductConceptRepository REGISTRADO" : "IProductConceptRepository NO REGISTRADO");
        Console.WriteLine("===============================================");
        Console.WriteLine();
    }

    private static void RegisterHttpClients(IServiceCollection services)
    {
        services.AddHttpClient<JumboProductOfferSearcher>(
            client => {
                ConfigureHttpClient(client, "https://www.jumbo.cl");
            });

        services.AddHttpClient<SantaIsabelProductOfferSearcher>(
            client => {
                ConfigureHttpClient(client, "https://www.santaisabel.cl");
            });

        services.AddHttpClient<TottusProductOfferSearcher>(
            client => {
                ConfigureHttpClient(client, "https://www.tottus.cl");
            });
    }

    private static void ConfigureHttpClient(HttpClient client, string baseAddress)
    {
        client.BaseAddress = new Uri(baseAddress);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/150.0.0.0 Safari/537.36");

        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es-CL,es;q=0.9");
        client.Timeout = TimeSpan.FromSeconds(20);
    }
}