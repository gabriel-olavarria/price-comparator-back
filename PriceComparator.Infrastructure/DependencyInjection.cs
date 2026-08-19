using Microsoft.Extensions.DependencyInjection;
using PriceComparator.Application.Interfaces.ProductOffers;
using PriceComparator.Infrastructure.Browsers;
using PriceComparator.Infrastructure.ProductOffers.Jumbo;
using PriceComparator.Infrastructure.ProductOffers.Lider;
using PriceComparator.Infrastructure.ProductOffers.Unimarc;
using PriceComparator.Infrastructure.Snapshots;

namespace PriceComparator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton< ISnapshotStore, FileSnapshotStore>();
        services.AddSingleton< PlaywrightHtmlBrowser>();
        services.AddSingleton< LiderProductParser>();
        services.AddSingleton< JumboProductParser>();
        services.AddSingleton<UnimarcProductParser>();
        services.AddHttpClient< JumboProductOfferSearcher>(client => {
                ConfigureHttpClient(client, "https://www.jumbo.cl");
        });
        services.AddTransient<IProductOfferSearcher, LiderProductOfferSearcher>();
        services.AddTransient<IProductOfferSearcher>(serviceProvider => serviceProvider .GetRequiredService< JumboProductOfferSearcher>());
        services.AddTransient<IProductOfferSearcher, UnimarcProductOfferSearcher>();
        
        return services;
    }

    private static void ConfigureHttpClient( HttpClient client, string baseAddress)
    {
        client.BaseAddress = new Uri(baseAddress);
        client.DefaultRequestHeaders .UserAgent
            .ParseAdd(
                "Mozilla/5.0 " +
                "(Macintosh; Intel Mac OS X 10_15_7) " +
                "AppleWebKit/537.36 " +
                "(KHTML, like Gecko) " +
                "Chrome/150.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders .AcceptLanguage .ParseAdd("es-CL,es;q=0.9");
        client.Timeout = TimeSpan.FromSeconds(20);
    }
}