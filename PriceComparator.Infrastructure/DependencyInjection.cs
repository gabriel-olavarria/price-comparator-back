using Microsoft.Extensions.DependencyInjection;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Infrastructure.ProductOffers.Jumbo;
using PriceComparator.Infrastructure.ProductOffers.SantaIsabel;
using PriceComparator.Infrastructure.ProductOffers.Tottus;
using PriceComparator.Infrastructure.ProductOffers.Unimarc; 

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
    }

    private static void RegisterHttpClients(IServiceCollection services)
    {
        // JUMBO - Sin cambios
        services.AddHttpClient<JumboProductOfferSearcher>(
            client =>
            {
                ConfigureHttpClient(client, "https://www.jumbo.cl");
            });

        // SANTA ISABEL - Sin cambios
        services.AddHttpClient<SantaIsabelProductOfferSearcher>(
            client =>
            {
                ConfigureHttpClient(client, "https://www.santaisabel.cl");
            });

        // TOTTUS - Sin cambios
        services.AddHttpClient<TottusProductOfferSearcher>(
            client =>
            { 
                ConfigureHttpClient(client, "https://www.tottus.cl");
            });

        // NUEVO: UNIMARC API
        services.AddHttpClient<UnimarcApiClient>(
            client =>
            {
                ConfigureHttpClient(client, "https://bff-unimarc-ecommerce.unimarc.cl/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.Timeout = TimeSpan.FromSeconds(30);
            });
    }

    private static void ConfigureHttpClient(HttpClient client, string baseAddress)
    {
        client.BaseAddress = new Uri(baseAddress);

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/150.0.0.0 Safari/537.36");

        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(
            "es-CL,es;q=0.9");

        client.Timeout = TimeSpan.FromSeconds(20);
    }
}
