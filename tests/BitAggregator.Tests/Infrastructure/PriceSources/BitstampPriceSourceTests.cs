using System.Net;
using BitAggregator.Infrastructure.PriceSources;

namespace BitAggregator.Tests.Infrastructure.PriceSources;

public sealed class BitstampPriceSourceTests
{
    private static readonly DateTime RequestedTimestamp = new(2023, 1, 1, 1, 0, 0, DateTimeKind.Utc);

    private static BitstampPriceSource CreateSource(MockHttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.bitstamp.net")
        };

        return new BitstampPriceSource(client);
    }

    [Fact]
    public async Task GetClosePriceAsync_ReturnsClosePriceForRequestedCandle()
    {
        const string json = """
            {
                "data": {
                    "ohlc": [
                        {
                            "timestamp": "1672531200",
                            "close": "16531.00"
                        }
                    ]
                }
            }
            """;

        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.JsonResponse(json));

        var source = CreateSource(handler);

        var result = await source.GetClosePriceAsync(
            "BTC/USD", RequestedTimestamp);

        Assert.NotNull(result);
        Assert.Equal("Bitstamp", result.SourceName);
        Assert.Equal(16531.00, result.Price);

        var uri = handler.LastRequest!.RequestUri!.ToString();

        Assert.Contains("/api/v2/ohlc/btcusd/", uri);
        Assert.Contains("step=3600", uri);
        Assert.Contains("limit=1", uri);
        Assert.Contains("start=1672531200", uri);
        Assert.DoesNotContain("end=", uri);
    }

    [Fact]
    public async Task GetClosePriceAsync_ReturnsNullForEmptyCandles()
    {
        const string json = """
            { "data": { "ohlc": [] } }
            """;

        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.JsonResponse(json));

        var source = CreateSource(handler);

        var result = await source.GetClosePriceAsync("BTC/USD", RequestedTimestamp);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetClosePriceAsync_ReturnsNullForDifferentCandle()
    {
        const string json = """
            {
                "data": {
                    "ohlc": [
                        {
                            "timestamp": "1672534800",
                            "close": "16531.00"
                        }
                     ]
                }
            }
            """;

        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.JsonResponse(json));

        var source = CreateSource(handler);

        var result = await source.GetClosePriceAsync("BTC/USD", RequestedTimestamp);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("""{"data":{}}""")]
    [InlineData("""{"data":{"ohlc":"invalid"}}""")]
    [InlineData("""{"data":{"ohlc":[{"timestamp":"invalid","close":"16531"}]}}""")]
    [InlineData("""{"data":{"ohlc":[{"timestamp":"1672531200","close":"invalid"}]}}""")]
    [InlineData("""{"data":{"ohlc":[{"timestamp":"1672531200","close":"-1"}]}}""")]
    public async Task GetClosePriceAsync_RejectsMalformedCandleData(string json)
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.JsonResponse(json));

        var source = CreateSource(handler);

        await Assert.ThrowsAsync<FormatException>(() => source.GetClosePriceAsync("BTC/USD", RequestedTimestamp));
    }

    [Fact]
    public async Task GetClosePriceAsync_RejectsInvalidJson()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.JsonResponse("{invalid-json}"));

        var source = CreateSource(handler);

        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => source.GetClosePriceAsync("BTC/USD", RequestedTimestamp));
    }

    [Fact]
    public async Task GetClosePriceAsync_PropagatesHttpErrors()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var source = CreateSource(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => source.GetClosePriceAsync("BTC/USD", RequestedTimestamp));
    }

    [Fact]
    public async Task GetClosePriceAsync_RejectsUnsupportedInstrument()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.JsonResponse("{}"));

        var source = CreateSource(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => source.GetClosePriceAsync("ETH/USD", RequestedTimestamp));

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task GetClosePriceAsync_RejectsNonHourlyTimestamp()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.JsonResponse("{}"));

        var source = CreateSource(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => source.GetClosePriceAsync("BTC/USD", RequestedTimestamp.AddMinutes(30)));

        Assert.Null(handler.LastRequest);
    }
}