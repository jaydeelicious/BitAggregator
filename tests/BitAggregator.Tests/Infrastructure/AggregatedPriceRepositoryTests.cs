using BitAggregator.Domain.Entities;
using BitAggregator.Infrastructure.Repositories;
using BitAggregator.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace BitAggregator.Tests.Infrastructure;

public sealed class AggregatedPriceRepositoryTests
{
    private static AggregatedPrice CreateAggregate(
        string instrument,
        DateTime timestamp,
        double price = 16530.25)
    {
        return new AggregatedPrice
        {
            Instrument = instrument,
            Timestamp = timestamp,
            Price = price,
            SourcePrices =
            [
                new SourcePrice
                {
                    SourceName = "Bitstamp",
                    Price = 16531.00
                },
                new SourcePrice
                {
                    SourceName = "Bitfinex",
                    Price = 16529.50
                }
            ]
        };
    }

    [Fact]
    public async Task AddAsync_PersistsAggregateAndSourcePrices()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = new AggregatedPriceRepository(database.Context);

        var timestamp = new DateTime(
            2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var aggregate = CreateAggregate("BTC/USD", timestamp);

        await repository.AddAsync(aggregate);

        var saved = await database.Context.AggregatedPrices
            .AsNoTracking()
            .Include(x => x.SourcePrices)
            .SingleAsync();

        Assert.Equal("BTC/USD", saved.Instrument);
        Assert.Equal(timestamp, saved.Timestamp);
        Assert.Equal(16530.25, saved.Price);
        Assert.Equal(2, saved.SourcePrices.Count);
    }

    [Fact]
    public async Task GetByInstrumentAndTimestampAsync_ReturnsSavedAggregate()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = new AggregatedPriceRepository(database.Context);

        var timestamp = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await repository.AddAsync(CreateAggregate("BTC/USD", timestamp));

        var result = await repository.GetByInstrumentAndTimestampAsync(
            "BTC/USD", timestamp);

        Assert.NotNull(result);
        Assert.Equal(16530.25, result.Price);
        Assert.Equal(2, result.SourcePrices.Count);
    }

    [Fact]
    public async Task GetByInstrumentAndTimestampAsync_ReturnsNullWhenMissing()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = new AggregatedPriceRepository(database.Context);

        var result = await repository.GetByInstrumentAndTimestampAsync(
            "BTC/USD",
            new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByTimeRangeAsync_FiltersAndOrdersRecords()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = new AggregatedPriceRepository(database.Context);

        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await repository.AddAsync(CreateAggregate("BTC/USD", start.AddHours(3)));

        await repository.AddAsync(CreateAggregate("BTC/USD", start.AddHours(1)));

        await repository.AddAsync(CreateAggregate("BTC/USD", start.AddHours(2)));

        await repository.AddAsync(CreateAggregate("ETH/USD", start.AddHours(2)));

        var results = await repository.GetByTimeRangeAsync(
            "BTC/USD",
            start.AddHours(1),
            start.AddHours(2));

        Assert.Equal(2, results.Count);
        Assert.Equal(start.AddHours(1), results[0].Timestamp);
        Assert.Equal(start.AddHours(2), results[1].Timestamp);
        Assert.All(results, x => Assert.Equal("BTC/USD", x.Instrument));
    }

    [Fact]
    public async Task AddAsync_RejectsDuplicateInstrumentAndTimestamp()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = new AggregatedPriceRepository(database.Context);

        var timestamp = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await repository.AddAsync(CreateAggregate("BTC/USD", timestamp));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            repository.AddAsync(CreateAggregate("BTC/USD", timestamp)));
    }

    [Fact]
    public async Task DeletingAggregate_CascadesToSourcePrices()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = new AggregatedPriceRepository(database.Context);

        var timestamp = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await repository.AddAsync(CreateAggregate("BTC/USD", timestamp));

        var aggregate = await database.Context.AggregatedPrices.SingleAsync();

        database.Context.AggregatedPrices.Remove(aggregate);
        await database.Context.SaveChangesAsync();

        var sourceCount = await database.Context.SourcePrices.CountAsync();

        Assert.Equal(0, sourceCount);
    }
}