
using BitAggregator.Application.Exceptions;
using BitAggregator.Application.Models;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BitAggregator.Tests.Integration;

public sealed class PricesControllerTests
{
    private const string Endpoint =
        "/api/prices/BTCUSD?timestamp=2023-01-01T01:00:00Z";

    [Fact]
    public async Task GetPrice_ValidRequest_ReturnsAggregatedPrice()
    {
        using var factory = new BitAggregatorApiFactory();

        factory.AggregationService
            .Setup(x => x.GetPriceAsync(
                "BTC/USD",
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AggregatedPriceResult(
                "BTC/USD",
                new DateTime(2023, 1, 1, 1, 0, 0, DateTimeKind.Utc),
                16530.25,
                PriceResultOrigin.ExternalApis,
                [
                    new SourcePriceResult("Bitstamp", 16531),
                    new SourcePriceResult("Bitfinex", 16529.50)
                ]));

        using var client = factory.CreateClient();

        var response = await client.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<
            System.Text.Json.JsonElement>();

        Assert.Equal("BTC/USD",
            json.GetProperty("instrument").GetString());

        Assert.Equal(16530.25,
            json.GetProperty("aggregatedPrice").GetDouble());

        Assert.Equal("ExternalApis",
            json.GetProperty("resultSource").GetString());

        Assert.Equal(2,
            json.GetProperty("sourcePrices").GetArrayLength());
    }

    [Theory]
    [InlineData("/api/prices/ETHUSD?timestamp=2023-01-01T01:00:00Z")]
    [InlineData("/api/prices/BTCUSD?timestamp=2023-01-01T01:30:00Z")]
    [InlineData("/api/prices/BTCUSD?timestamp=2023-01-01T01:00:00%2B02:00")]
    [InlineData("/api/prices/BTCUSD")]
    [InlineData("/api/prices/BTCUSD?timestamp=2030-01-01T00:00:00Z")]
    [InlineData("/api/prices/BTCUSD?timestamp=not-a-date")]
    public async Task GetPrice_InvalidRequest_ReturnsValidationProblem(
        string url)
    {
        using var factory = new BitAggregatorApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<
            System.Text.Json.JsonElement>();

        Assert.Equal(400,
            json.GetProperty("status").GetInt32());

        Assert.True(
            json.TryGetProperty("errors", out var errors));

        Assert.True(
            errors.EnumerateObject().Any());
    }

    [Fact]
    public async Task GetPrice_ExternalFailure_ReturnsProblemDetails()
    {
        using var factory = new BitAggregatorApiFactory();

        factory.AggregationService
            .Setup(x => x.GetPriceAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException(
                "External exchange unavailable"));

        using var client = factory.CreateClient();

        var response = await client.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<
            System.Text.Json.JsonElement>();

        Assert.Equal(502,
            json.GetProperty("status").GetInt32());

        Assert.Equal(
            "Price provider unavailable",
            json.GetProperty("title").GetString());
    }


    [Fact]
    public async Task GetPrice_MissingSource_ReturnsBadGateway()
    {
        using var factory = new BitAggregatorApiFactory();

        factory.AggregationService
            .Setup(x => x.GetPriceAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PriceSourceUnavailableException(
                "Bitfinex returned no matching candle."));

        using var client = factory.CreateClient();

        var response = await client.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(502, json.GetProperty("status").GetInt32());

        Assert.Equal(
            "Price provider unavailable",
            json.GetProperty("title").GetString());
    }


    [Fact]
    public async Task GetPrice_UnexpectedFailure_ReturnsInternalServerError()
    {
        using var factory = new BitAggregatorApiFactory();

        factory.AggregationService
            .Setup(x => x.GetPriceAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Unexpected application failure."));

        using var client = factory.CreateClient();

        var response = await client.GetAsync(Endpoint);

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(500, json.GetProperty("status").GetInt32());

        Assert.Equal(
            "An unexpected error occurred",
            json.GetProperty("title").GetString());
    }


}
