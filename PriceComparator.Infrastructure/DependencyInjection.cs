
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Persistence;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Common;
using PriceComparator.Infrastructure.Persistence;
using PriceComparator.Infrastructure.Persistence.Configurations;
using PriceComparator.Infrastructure.Persistence.Contexts;
using PriceComparator.Infrastructure.Persistence.Repositories;
using PriceComparator.Infrastructure.ProductOffers.Acuenta;
using PriceComparator.Infrastructure.ProductOffers.Jumbo;
using PriceComparator.Infrastructure.ProductOffers.Lider;
using PriceComparator.Infrastructure.ProductOffers.SantaIsabel;
using PriceComparator.Infrastructure.ProductOffers.Tottus;
using PriceComparator.Infrastructure.Security;

namespace PriceComparator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        RegisterDependencies(services);
        RegisterDbContext(services);
        RegisterHttpClients(services);
        RegisterTurnstile(services, configuration);
        RegisterLiderGraphQLClient(services);
        RegisterSupermarketExtraction(services, configuration);
        return services;
    }

    private static void RegisterDependencies(IServiceCollection services)
    {
        
        services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.Scan(scan => scan.FromAssemblies(typeof(LiderGraphQLClient).Assembly)

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
        services.AddHttpClient<JumboProductOfferSearcher>(
            client => ConfigureHttpClient(client, "https://www.jumbo.cl"));

        services.AddHttpClient<SantaIsabelProductOfferSearcher>(
            client => ConfigureHttpClient(client, "https://www.santaisabel.cl"));

        services.AddHttpClient<TottusProductOfferSearcher>(
            client => ConfigureHttpClient(client, "https://www.tottus.cl"));
        
        services.AddHttpClient<AcuentaProductOfferSearcher>(
            client => ConfigureHttpClient(client, "https://www.acuenta.cl"));
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

    private static void RegisterTurnstile(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TurnstileOptions>(configuration.GetSection(TurnstileOptions.SectionName));
        services.AddHttpClient<ITurnstileVerificationService, TurnstileVerificationService>(
            client =>
            {
                client.BaseAddress = new Uri("https://challenges.cloudflare.com/");
                client.Timeout = TimeSpan.FromSeconds(10);
            });
    }

    private static void RegisterLiderGraphQLClient(IServiceCollection services)
    {
        services.AddHttpClient<LiderGraphQLClient>(client =>
        {
            ConfigureHttpClient(client, "https://super.lider.cl");
            client.DefaultRequestVersion = HttpVersion.Version11;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;

            // SearchAsync controla el tiempo máximo de 45 segundos.
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
            new SocketsHttpHandler
            {
                AutomaticDecompression =
                    DecompressionMethods.GZip |
                    DecompressionMethods.Deflate |
                    DecompressionMethods.Brotli,

                AllowAutoRedirect = false,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });
    }
    
    private static void RegisterDbContext(IServiceCollection services)
    {
        services.AddDbContext<PriceComparatorDbContext>();
    }
    
    private static void RegisterSupermarketExtraction(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SupermarketExtractionOptions>(
            configuration.GetSection("SupermarketExtraction"));
    }
}
