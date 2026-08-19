using Microsoft.Extensions.DependencyInjection;
using PriceComparator.Application.UseCases.ProductOffers.SearchProductOffers;

namespace PriceComparator.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<ISearchProductOffersUseCase, SearchProductOffersUseCase>();

        return services;
    }
}