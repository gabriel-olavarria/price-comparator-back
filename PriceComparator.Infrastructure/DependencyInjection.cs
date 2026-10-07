using Microsoft.Extensions.DependencyInjection;
using PriceComparator.Application.Interfaces.ProductOffers;
using PriceComparator.Infrastructure.Browsers;
using PriceComparator.Infrastructure.ProductOffers.Jumbo;
using PriceComparator.Infrastructure.ProductOffers.Lider;
using PriceComparator.Infrastructure.ProductOffers.SantaIsabel;
using PriceComparator.Infrastructure.ProductOffers.Tottus;
using PriceComparator.Infrastructure.ProductOffers.Unimarc;
using PriceComparator.Infrastructure.Storage;

namespace PriceComparator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Servicios compartidos
        services.AddSingleton<PlaywrightHtmlBrowser>();
        services.AddSingleton<JsonProductStore>();

        // UNIMARC
        services.AddSingleton<UnimarcProductParser>();
        services.AddTransient<IProductOfferSearcher, UnimarcProductOfferSearcher>();

        // JUMBO
        services.AddHttpClient<JumboProductOfferSearcher>(client => { ConfigureHttpClient(client, "https://www.jumbo.cl"); });
        services.AddSingleton<JumboProductParser>();
        services.AddTransient<IProductOfferSearcher>(serviceProvider => serviceProvider.GetRequiredService<JumboProductOfferSearcher>());

        // SANTA ISABEL
        services.AddHttpClient<SantaIsabelProductOfferSearcher>(client => { ConfigureHttpClient(client, "https://www.santaisabel.cl"); });
        services.AddSingleton<SantaIsabelProductParser>();
        services.AddTransient<IProductOfferSearcher>(serviceProvider => serviceProvider.GetRequiredService<SantaIsabelProductOfferSearcher>());

        // LIDER
        services.AddSingleton<LiderProductParser>();
        services.AddTransient<IProductOfferSearcher, LiderProductOfferSearcher>();

        // TOTTUS
        services.AddHttpClient<TottusProductOfferSearcher>(client =>
        {
            ConfigureHttpClient(
                client,
                "https://www.tottus.cl");
        });

        services.AddSingleton<TottusProductParser>();
        services.AddSingleton<TottusCategoryParser>();
        services.AddSingleton<TottusCategoryResolver>();

        services.AddTransient<IProductOfferSearcher>(
            serviceProvider =>
                serviceProvider.GetRequiredService<TottusProductOfferSearcher>());
        
        return services;
    }

    private static void ConfigureHttpClient(HttpClient client, string baseAddress)
    {
        client.BaseAddress = new Uri(baseAddress);

        client.DefaultRequestHeaders
            .UserAgent
            .ParseAdd(
                "Mozilla/5.0 " +
                "(Macintosh; Intel Mac OS X 10_15_7) " +
                "AppleWebKit/537.36 " +
                "(KHTML, like Gecko) " +
                "Chrome/150.0.0.0 Safari/537.36");

        client.DefaultRequestHeaders
            .AcceptLanguage
            .ParseAdd("es-CL,es;q=0.9");

        client.Timeout = TimeSpan.FromSeconds(20);
    }
}