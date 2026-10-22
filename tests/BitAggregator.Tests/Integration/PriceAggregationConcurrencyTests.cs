
using BitAggregator.Application.Aggregation;
using BitAggregator.Application.Interfaces;
using BitAggregator.Application.Models;
using BitAggregator.Application.Services;
using BitAggregator.Infrastructure.Persistence;
using BitAggregator.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BitAggregator.Tests.Integration;

public sealed class PriceAggregationConcurrencyTests
{
    [Fact]
    public async Task ConcurrentRequests_DoNotCreateDuplicateAggregates()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"bitaggregator-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<BitAggregatorDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            await using (var setup = new BitAggregatorDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
            }

            await using var context1 = new BitAggregatorDbContext(options);
            await using var context2 = new BitAggregatorDbContext(options);

            // Make both requests reach the provider-fetching stage
            // before either is allowed to persist its aggregate.
            var coordinator = new FetchCoordinator(4);

            IPriceSource[] sources =
            [
                new CoordinatedPriceSource("Bitstamp", 16531, coordinator),
                new CoordinatedPriceSource("Bitfinex", 16529.50, coordinator)
            ];

            var aggregator = new ArithmeticMeanAggregator();

            var service1 = new PriceAggregationService(
                new AggregatedPriceRepository(context1),
                sources,
                aggregator);

            var service2 = new PriceAggregationService(
                new AggregatedPriceRepository(context2),
                sources,
                aggregator);

            var timestamp = new DateTime(2023, 1, 1, 1, 0, 0, DateTimeKind.Utc);

            var request1 = service1.GetPriceAsync("BTC/USD", timestamp);
            var request2 = service2.GetPriceAsync("BTC/USD", timestamp);

            var results = await Task.WhenAll(request1, request2)
                .WaitAsync(TimeSpan.FromSeconds(15));

            Assert.All(results, result =>
            {
                Assert.Equal(16530.25, result.AggregatedPrice);
                Assert.Equal(2, result.SourcePrices.Count);
            });

            Assert.Contains(results,
                x => x.Origin == PriceResultOrigin.ExternalApis);

            Assert.Contains(results,
                x => x.Origin == PriceResultOrigin.Datastore);

            await using var verification =
                new BitAggregatorDbContext(options);

            Assert.Equal(
                1,
                await verification.AggregatedPrices.CountAsync());

            Assert.Equal(
                2,
                await verification.SourcePrices.CountAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

    private sealed class FetchCoordinator(int expectedCalls)
    {
        private int _calls;

        private readonly TaskCompletionSource _allStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task WaitForOtherCallsAsync()
        {
            if (Interlocked.Increment(ref _calls) == expectedCalls)
                _allStarted.TrySetResult();

            await _allStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(10));
        }
    }

    private sealed class CoordinatedPriceSource(
        string name,
        double price,
        FetchCoordinator coordinator) : IPriceSource
    {
        public string Name => name;

        public async Task<SourcePriceResult?> GetClosePriceAsync(
            string instrument,
            DateTime timestamp,
            CancellationToken cancellationToken = default)
        {
            await coordinator.WaitForOtherCallsAsync();

            return new SourcePriceResult(Name, price);
        }
    }
}
