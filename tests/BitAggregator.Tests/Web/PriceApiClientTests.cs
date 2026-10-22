
using System.Net;
using System.Text;
using BitAggregator.Web.Services;

namespace BitAggregator.Tests.Web;

public sealed class PriceApiClientTests
{
    [Fact]
    public async Task GetPriceAsync_UsesExpectedUrlAndDeserializesResponse()
    {
        Uri? requestedUri = null;

        using var httpClient = new HttpClient(
            new StubHandler(request =>
            {
                requestedUri = request.RequestUri;

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "instrument": "BTC/USD",
                          "timestamp": "2023-01-01T01:00:00Z",
                          "aggregatedPrice": 16530.25,
                          "resultSource": "ExternalApis",
                          "sourcePrices": [
                            { "source": "Bitstamp", "price": 16531.0 },
                            { "source": "Bitfinex", "price": 16529.5 }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }))
        {
            BaseAddress = new Uri("https://localhost:7017/")
        };

        var apiClient = new PriceApiClient(httpClient);

        var result = await apiClient.GetPriceAsync(
            "BTCUSD",
            new DateTimeOffset(2023, 1, 1, 1, 0, 0, TimeSpan.Zero));

        Assert.NotNull(requestedUri);
        Assert.Equal(
            "/api/prices/BTCUSD",
            requestedUri.AbsolutePath);

        Assert.Contains(
            "timestamp=2023-01-01T01%3A00%3A00Z",
            requestedUri.Query);

        Assert.Equal("BTC/USD", result.Instrument);
        Assert.Equal(16530.25, result.AggregatedPrice);
        Assert.Equal("ExternalApis", result.ResultSource);
        Assert.Equal(2, result.SourcePrices.Count);
    }

    [Fact]
    public async Task GetHistoryAsync_DeserializesEmptyCollection()
    {
        using var httpClient = new HttpClient(
            new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "[]",
                    Encoding.UTF8,
                    "application/json")
            }))
        {
            BaseAddress = new Uri("https://localhost:7017/")
        };

        var apiClient = new PriceApiClient(httpClient);

        var results = await apiClient.GetHistoryAsync(
            "BTCUSD",
            new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2023, 1, 2, 0, 0, 0, TimeSpan.Zero));

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetPriceAsync_HttpFailure_ThrowsHttpRequestException()
    {
        using var httpClient = new HttpClient(
            new StubHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.BadRequest)))
        {
            BaseAddress = new Uri("https://localhost:7017/")
        };

        var apiClient = new PriceApiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            apiClient.GetPriceAsync("BTCUSD", new DateTimeOffset(2026, 1, 1, 1, 0, 0, TimeSpan.Zero)));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    }


    [Fact]
    public async Task GetPriceAsync_ValidationProblem_UsesBackendMessage()
    {
        using var httpClient = new HttpClient(
            new StubHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        """
                    {
                      "title": "Invalid price lookup request",
                      "status": 400,
                      "errors": {
                        "timestamp": [
                          "Timestamp must be aligned to a full hour."
                        ]
                      }
                    }
                    """,
                        Encoding.UTF8,
                        "application/problem+json")
                }))
        {
            BaseAddress = new Uri("https://localhost:7017/")
        };

        var apiClient = new PriceApiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            apiClient.GetPriceAsync(
                "BTCUSD",
                new DateTimeOffset(
                    2026, 1, 1, 1, 30, 0, TimeSpan.Zero)));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);

        Assert.Equal(
            "Timestamp must be aligned to a full hour.",
            exception.Message);
    }


    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}
