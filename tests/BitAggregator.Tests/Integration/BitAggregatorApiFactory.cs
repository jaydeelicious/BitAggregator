
using BitAggregator.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Moq;

namespace BitAggregator.Tests.Integration;

public sealed class BitAggregatorApiFactory
    : WebApplicationFactory<Program>
{
    public Mock<IPriceAggregationService> AggregationService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPriceAggregationService>();

            services.AddSingleton(
                AggregationService.Object);
        });
    }
}
