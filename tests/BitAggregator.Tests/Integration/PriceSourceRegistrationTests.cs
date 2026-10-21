
using BitAggregator.Application.Interfaces;
using BitAggregator.Infrastructure.PriceSources;
using Microsoft.Extensions.DependencyInjection;

namespace BitAggregator.Tests.Integration;

public sealed class PriceSourceRegistrationTests
{
    [Fact]
    public void DependencyInjection_RegistersBothPriceSources()
    {
        using var factory = new BitAggregatorApiFactory();

        using var scope = factory.Services.CreateScope();

        var sources = scope.ServiceProvider
            .GetRequiredService<IEnumerable<IPriceSource>>()
            .ToArray();

        Assert.Equal(2, sources.Length);

        Assert.Contains(sources,
            source => source is BitstampPriceSource);

        Assert.Contains(sources,
            source => source is BitfinexPriceSource);
    }
}
