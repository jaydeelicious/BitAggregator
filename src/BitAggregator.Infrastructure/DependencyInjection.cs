using BitAggregator.Application.Interfaces;
using BitAggregator.Infrastructure.PriceSources;
using BitAggregator.Infrastructure.Persistence;
using BitAggregator.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BitAggregator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string is not configured.");
        }

        services.AddDbContext<BitAggregatorDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IAggregatedPriceRepository, AggregatedPriceRepository>();

        services.AddHttpClient<IPriceSource, BitstampPriceSource>(
            client =>
            {
                client.BaseAddress = new Uri("https://www.bitstamp.net/");

                client.Timeout = TimeSpan.FromSeconds(10);
            });

        services.AddHttpClient<IPriceSource, BitfinexPriceSource>(
            client =>
            {
                client.BaseAddress = new Uri("https://api-pub.bitfinex.com/");

                client.Timeout = TimeSpan.FromSeconds(10);
            });

        return services;
    }
}