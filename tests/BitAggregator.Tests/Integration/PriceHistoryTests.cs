
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BitAggregator.Domain.Entities;
using Moq;

namespace BitAggregator.Tests.Integration;

public sealed class PriceHistoryTests
{
    private const string Endpoint =
        "/api/prices/BTCUSD/history" +
        "?from=2023-01-01T00:00:00Z" +
        "&to=2023-01-02T00:00:00Z";

    [Fact]
    public async Task GetHistory_ReturnsPersistedPricesOrderedByTimestamp()
    {
        using var factory = new BitAggregatorApiFactory();

        var firstTimestamp = new DateTime(
            2023, 1, 1, 1, 0, 0, DateTimeKind.Utc);

        var secondTimestamp = firstTimestamp.AddHours(1);

        // Deliberately return records out of order.
        var prices = new List<AggregatedPrice>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Instrument = "BTC/USD",
                Timestamp = secondTimestamp,
                Price = 16600,
                SourcePrices = []
            },
            new()
            {
                Id = Guid.NewGuid(),
                Instrument = "BTC/USD",
                Timestamp = firstTimestamp,
                Price = 16530.25,
                SourcePrices = []
            }
        };

        factory.PriceRepository
            .Setup(x => x.GetByTimeRangeAsync(
                "BTC/USD",
                new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2023, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(prices);

        using var client = factory.CreateClient();

        var response = await client.GetAsync(Endpoint);

        factory.PriceRepository.Verify(
            x => x.GetByTimeRangeAsync(
                "BTC/USD",
                new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2023, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(2, json.GetArrayLength());

        Assert.Equal(
            16530.25,
            json[0].GetProperty("aggregatedPrice").GetDouble());

        Assert.Equal(
            16600,
            json[1].GetProperty("aggregatedPrice").GetDouble());

        Assert.All(
            json.EnumerateArray(),
            item => Assert.Equal(
                "Datastore",
                item.GetProperty("resultSource").GetString()));

        factory.AggregationService.Verify(
            x => x.GetPriceAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetHistory_NoPersistedPrices_ReturnsEmptyArray()
    {
        using var factory = new BitAggregatorApiFactory();

        factory.PriceRepository
            .Setup(x => x.GetByTimeRangeAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AggregatedPrice>());

        using var client = factory.CreateClient();

        var response = await client.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, json.GetArrayLength());
    }

    [Theory]
    [InlineData("/api/prices/ETHUSD/history?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z")]
    [InlineData("/api/prices/BTCUSD/history?from=2023-01-03T00:00:00Z&to=2023-01-02T00:00:00Z")]
    [InlineData("/api/prices/BTCUSD/history?from=2023-01-01T00:30:00Z&to=2023-01-02T00:00:00Z")]
    [InlineData("/api/prices/BTCUSD/history?from=2023-01-01T00:00:00%2B02:00&to=2023-01-02T00:00:00Z")]
    [InlineData("/api/prices/BTCUSD/history?to=2023-01-02T00:00:00Z")]
    [InlineData("/api/prices/BTCUSD/history?from=2023-01-01T00:00:00Z")]
    public async Task GetHistory_InvalidRequest_ReturnsValidationProblem(
        string url)
    {
        using var factory = new BitAggregatorApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(400, json.GetProperty("status").GetInt32());
        Assert.True(json.TryGetProperty("errors", out _));

        factory.PriceRepository.Verify(
            x => x.GetByTimeRangeAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetHistory_EqualRangeBoundaries_AreAllowed()
    {
        using var factory = new BitAggregatorApiFactory();

        factory.PriceRepository
            .Setup(x => x.GetByTimeRangeAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AggregatedPrice>());

        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/prices/BTCUSD/history" +
            "?from=2023-01-01T01:00:00Z" +
            "&to=2023-01-01T01:00:00Z");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
