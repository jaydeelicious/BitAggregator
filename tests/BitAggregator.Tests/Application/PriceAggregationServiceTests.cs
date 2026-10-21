
using BitAggregator.Application.Aggregation;
using BitAggregator.Application.Exceptions;
using BitAggregator.Application.Interfaces;
using BitAggregator.Application.Models;
using BitAggregator.Application.Services;
using BitAggregator.Domain.Entities;
using Moq;

namespace BitAggregator.Tests.Application;

public sealed class PriceAggregationServiceTests
{
    private static readonly DateTime Timestamp =
        new(2023, 1, 1, 1, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IAggregatedPriceRepository> _repository = new();

    private static Mock<IPriceSource> CreateSource(
        string name,
        double price)
    {
        var source = new Mock<IPriceSource>();

        source.SetupGet(x => x.Name).Returns(name);

        source.Setup(x => x.GetClosePriceAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SourcePriceResult(name, price));

        return source;
    }

    private PriceAggregationService CreateService(
        params IPriceSource[] sources)
    {
        return new PriceAggregationService(
            _repository.Object,
            sources,
            new ArithmeticMeanAggregator());
    }

    [Fact]
    public async Task GetPriceAsync_CacheHit_DoesNotCallSources()
    {
        var cached = new AggregatedPrice
        {
            Instrument = "BTC/USD",
            Timestamp = Timestamp,
            Price = 16530.25,
            SourcePrices =
            [
                new SourcePrice
                {
                    SourceName = "Bitstamp",
                    Price = 16531
                },
                new SourcePrice
                {
                    SourceName = "Bitfinex",
                    Price = 16529.50
                }
            ]
        };

        _repository.Setup(x => x.GetByInstrumentAndTimestampAsync(
                "BTC/USD", Timestamp, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var bitstamp = CreateSource("Bitstamp", 16531);
        var bitfinex = CreateSource("Bitfinex", 16529.50);

        var service = CreateService(
            bitstamp.Object,
            bitfinex.Object);

        var result = await service.GetPriceAsync("BTC/USD", Timestamp);

        Assert.Equal(PriceResultOrigin.Datastore, result.Origin);
        Assert.Equal(16530.25, result.AggregatedPrice);
        Assert.Equal(2, result.SourcePrices.Count);

        bitstamp.Verify(x => x.GetClosePriceAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Never);

        bitfinex.Verify(x => x.GetClosePriceAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _repository.Verify(x => x.AddAsync(
            It.IsAny<AggregatedPrice>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPriceAsync_CacheMiss_AggregatesAndPersistsPrices()
    {
        var bitstamp = CreateSource("Bitstamp", 16531);
        var bitfinex = CreateSource("Bitfinex", 16529.50);

        AggregatedPrice? savedEntity = null;

        _repository.Setup(x => x.AddAsync(
                It.IsAny<AggregatedPrice>(),
                It.IsAny<CancellationToken>()))
            .Callback<AggregatedPrice, CancellationToken>(
                (entity, _) => savedEntity = entity)
            .Returns(Task.CompletedTask);

        var service = CreateService(
            bitstamp.Object,
            bitfinex.Object);

        var result = await service.GetPriceAsync("BTC/USD", Timestamp);

        Assert.Equal(PriceResultOrigin.ExternalApis, result.Origin);
        Assert.Equal(16530.25, result.AggregatedPrice);
        Assert.Equal(2, result.SourcePrices.Count);

        Assert.NotNull(savedEntity);
        Assert.Equal("BTC/USD", savedEntity.Instrument);
        Assert.Equal(Timestamp, savedEntity.Timestamp);
        Assert.Equal(16530.25, savedEntity.Price);
        Assert.Equal(2, savedEntity.SourcePrices.Count);

        Assert.Contains(savedEntity.SourcePrices,
            x => x.SourceName == "Bitstamp" && x.Price == 16531);

        Assert.Contains(savedEntity.SourcePrices,
            x => x.SourceName == "Bitfinex" && x.Price == 16529.50);

        _repository.Verify(x => x.AddAsync(
            It.IsAny<AggregatedPrice>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPriceAsync_MissingSource_DoesNotPersist()
    {
        var bitstamp = CreateSource("Bitstamp", 16531);

        var bitfinex = new Mock<IPriceSource>();
        bitfinex.SetupGet(x => x.Name).Returns("Bitfinex");

        bitfinex.Setup(x => x.GetClosePriceAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SourcePriceResult?)null);

        var service = CreateService(
            bitstamp.Object,
            bitfinex.Object);

        await Assert.ThrowsAsync<PriceSourceUnavailableException>(() =>
            service.GetPriceAsync("BTC/USD", Timestamp));

        _repository.Verify(x => x.AddAsync(
            It.IsAny<AggregatedPrice>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPriceAsync_SourceFailure_DoesNotPersist()
    {
        var bitstamp = CreateSource("Bitstamp", 16531);

        var bitfinex = new Mock<IPriceSource>();

        bitfinex.Setup(x => x.GetClosePriceAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Bitfinex unavailable"));

        var service = CreateService(
            bitstamp.Object,
            bitfinex.Object);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetPriceAsync("BTC/USD", Timestamp));

        _repository.Verify(x => x.AddAsync(
            It.IsAny<AggregatedPrice>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }


    [Fact]
    public async Task GetPriceAsync_CacheMiss_FetchesSourcesConcurrently()
    {
        var bitstampCompletion =
            new TaskCompletionSource<SourcePriceResult?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var bitfinexCompletion =
            new TaskCompletionSource<SourcePriceResult?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var bitstamp = new Mock<IPriceSource>();
        var bitfinex = new Mock<IPriceSource>();

        bitstamp.Setup(x => x.GetClosePriceAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(() => bitstampCompletion.Task);

        bitfinex.Setup(x => x.GetClosePriceAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(() => bitfinexCompletion.Task);

        var service = CreateService(
            bitstamp.Object,
            bitfinex.Object);

        var aggregationTask = service.GetPriceAsync(
            "BTC/USD", Timestamp);

        try
        {
            bitstamp.Verify(x => x.GetClosePriceAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()), Times.Once);

            bitfinex.Verify(x => x.GetClosePriceAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()), Times.Once);

            bitstampCompletion.SetResult(
                new SourcePriceResult("Bitstamp", 16531));

            bitfinexCompletion.SetResult(
                new SourcePriceResult("Bitfinex", 16529.50));

            var result = await aggregationTask;

            Assert.Equal(16530.25, result.AggregatedPrice);
        }
        finally
        {
            bitstampCompletion.TrySetCanceled();
            bitfinexCompletion.TrySetCanceled();
        }
    }


    [Fact]
    public async Task GetPriceAsync_DuplicateInsert_ReturnsPersistedPrice()
    {
        var bitstamp = CreateSource("Bitstamp", 16531);
        var bitfinex = CreateSource("Bitfinex", 16529.50);

        var persisted = new AggregatedPrice
        {
            Instrument = "BTC/USD",
            Timestamp = Timestamp,
            Price = 16530.25,
            SourcePrices =
            [
                new SourcePrice
            {
                SourceName = "Bitstamp",
                Price = 16531
            },
            new SourcePrice
            {
                SourceName = "Bitfinex",
                Price = 16529.50
            }
            ]
        };

        _repository.SetupSequence(
                x => x.GetByInstrumentAndTimestampAsync(
                    "BTC/USD",
                    Timestamp,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((AggregatedPrice?)null)
            .ReturnsAsync(persisted);

        _repository.Setup(x => x.AddAsync(
                It.IsAny<AggregatedPrice>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new PriceAlreadyExistsException(
                    new Exception("UNIQUE constraint violation")));

        var service = CreateService(
            bitstamp.Object,
            bitfinex.Object);

        var result = await service.GetPriceAsync(
            "BTC/USD", Timestamp);

        Assert.Equal(PriceResultOrigin.Datastore, result.Origin);
        Assert.Equal(16530.25, result.AggregatedPrice);
        Assert.Equal(2, result.SourcePrices.Count);

        _repository.Verify(x => x.AddAsync(
            It.IsAny<AggregatedPrice>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPriceAsync_NoConfiguredSources_Throws()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetPriceAsync("BTC/USD", Timestamp));

        Assert.Equal(
            "No price sources are configured.",
            exception.Message);

        _repository.Verify(x => x.AddAsync(
            It.IsAny<AggregatedPrice>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPriceAsync_DuplicateInsertWithoutSavedRecord_Throws()
    {
        var bitstamp = CreateSource("Bitstamp", 16531);
        var bitfinex = CreateSource("Bitfinex", 16529.50);

        _repository.Setup(x => x.AddAsync(
                It.IsAny<AggregatedPrice>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new PriceAlreadyExistsException(
                    new Exception("UNIQUE constraint violation")));

        var service = CreateService(
            bitstamp.Object,
            bitfinex.Object);

        await Assert.ThrowsAsync<PriceAlreadyExistsException>(
            () => service.GetPriceAsync("BTC/USD", Timestamp));
    }

}
