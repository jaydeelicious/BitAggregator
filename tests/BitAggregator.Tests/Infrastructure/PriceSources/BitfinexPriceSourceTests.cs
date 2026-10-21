
using System.Net;
using System.Text.Json;
using BitAggregator.Infrastructure.PriceSources;

namespace BitAggregator.Tests.Infrastructure.PriceSources;

public sealed class BitfinexPriceSourceTests
{
    private static readonly DateTime RequestedTimestamp =
        new(2023, 1, 1, 1, 0, 0, DateTimeKind.Utc);

    private static BitfinexPriceSource CreateSource(
        MockHttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api-pub.bitfinex.com/")
        };

        return new BitfinexPriceSource(client);
    }

    [Fact]
    public async Task GetClosePriceAsync_ReturnsClosePriceForRequestedCandle()
    {
        const string json = """
            [
              [
                1672531200000,
                16530.00,
                16529.50,
                16550.00,
                16500.00,
                125.5
              ]
            ]
            """;

        var handler = new MockHttpMessageHandler(
            _ => MockHttpMessageHandler.JsonResponse(json));

        var source = CreateSource(handler);

        var result = await source.GetClosePriceAsync(
            "BTC/USD", RequestedTimestamp);

        Assert.NotNull(result);
        Assert.Equal("Bitfinex", result.SourceName);
        Assert.Equal(16529.50, result.Price);

        var uri = handler.LastRequest!.RequestUri!.ToString();

        Assert.Contains("/v2/candles/trade:1h:tBTCUSD/hist", uri);
        Assert.Contains("start=1672531200000", uri);
        Assert.Contains("end=1672534799999", uri);
        Assert.Contains("limit=1", uri);
    }

    [Fact]
    public async Task GetClosePriceAsync_ReturnsNullForEmptyResponse()
    {
        var handler = new MockHttpMessageHandler(
            _ => MockHttpMessageHandler.JsonResponse("[]"));

        var source = CreateSource(handler);

        var result = await source.GetClosePriceAsync(
            "BTC/USD", RequestedTimestamp);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetClosePriceAsync_ReturnsNullForDifferentCandle()
    {
        const string json = """
            [
              [
                1672534800000,
                16530,
                16529.50,
                16550,
                16500,
                125.5
              ]
            ]
            """;

        var handler = new MockHttpMessageHandler(
            _ => MockHttpMessageHandler.JsonResponse(json));

        var source = CreateSource(handler);

        var result = await source.GetClosePriceAsync(
            "BTC/USD", RequestedTimestamp);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("""{"error":"unexpected"}""")]
    [InlineData("""[[1672531200000,16530]]""")]
    [InlineData("""[["invalid",16530,16529.5,16550,16500,125.5]]""")]
    [InlineData("""[[1672531200000,16530,"invalid",16550,16500,125.5]]""")]
    [InlineData("""[[1672531200000,16530,-1,16550,16500,125.5]]""")]
    [InlineData("""[[1672531200000,16530,0,16550,16500,125.5]]""")]
    public async Task GetClosePriceAsync_RejectsMalformedResponses(
        string json)
    {
        var handler = new MockHttpMessageHandler(
            _ => MockHttpMessageHandler.JsonResponse(json));

        var source = CreateSource(handler);

        await Assert.ThrowsAsync<FormatException>(() =>
            source.GetClosePriceAsync("BTC/USD", RequestedTimestamp));
    }

    [Fact]
    public async Task GetClosePriceAsync_RejectsInvalidJson()
    {
        var handler = new MockHttpMessageHandler(
            _ => MockHttpMessageHandler.JsonResponse("[invalid-json"));

        var source = CreateSource(handler);

        await Assert.ThrowsAnyAsync<JsonException>(() =>
            source.GetClosePriceAsync("BTC/USD", RequestedTimestamp));
    }

    [Fact]
    public async Task GetClosePriceAsync_PropagatesHttpErrors()
    {
        var handler = new MockHttpMessageHandler(
            _ => new HttpResponseMessage(
                HttpStatusCode.ServiceUnavailable));

        var source = CreateSource(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            source.GetClosePriceAsync("BTC/USD", RequestedTimestamp));
    }

    [Fact]
    public async Task GetClosePriceAsync_RejectsUnsupportedInstrument()
    {
        var handler = new MockHttpMessageHandler(
            _ => MockHttpMessageHandler.JsonResponse("[]"));

        var source = CreateSource(handler);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            source.GetClosePriceAsync("ETH/USD", RequestedTimestamp));

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task GetClosePriceAsync_RejectsNonHourlyTimestamp()
    {
        var handler = new MockHttpMessageHandler(
            _ => MockHttpMessageHandler.JsonResponse("[]"));

        var source = CreateSource(handler);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            source.GetClosePriceAsync(
                "BTC/USD",
                RequestedTimestamp.AddMinutes(30)));

        Assert.Null(handler.LastRequest);
    }
}
