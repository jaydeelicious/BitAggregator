
using BitAggregator.Application.Aggregation;
using BitAggregator.Application.Interfaces;
using BitAggregator.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BitAggregator.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<IPriceAggregationService, PriceAggregationService>();

        services.AddSingleton<IPriceAggregator, ArithmeticMeanAggregator>();

        return services;
    }
}
