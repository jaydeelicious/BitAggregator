
using System.Net;
using System.Text;
using BitAggregator.Web.Pages;
using BitAggregator.Web.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BitAggregator.Tests.Web;

public sealed class HistoryTests : BunitContext
{
    [Fact]
    public void History_RendersDateRangeControls()
    {
        var cut = RenderHistory(_ => JsonResponse("[]"));

        Assert.NotNull(cut.Find("#from-date"));
        Assert.NotNull(cut.Find("#to-date"));

        Assert.Equal(24, cut.FindAll("#from-hour option").Count);
        Assert.Equal(24, cut.FindAll("#to-hour option").Count);

        Assert.Contains("Search persisted prices", cut.Markup);
    }

    [Fact]
    public void History_Success_DisplaysPersistedRecords()
    {
        var cut = RenderHistory(_ => JsonResponse(
            """
            [
              {
                "instrument": "BTC/USD",
                "timestamp": "2023-01-01T01:00:00Z",
                "aggregatedPrice": 16530.25,
                "resultSource": "Datastore",
                "sourcePrices": [
                  { "source": "Bitstamp", "price": 16531.0 },
                  { "source": "Bitfinex", "price": 16529.5 }
                ]
              }
            ]
            """));

        cut.Find(".history-button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("2023-01-01 01:00", cut.Markup);
            Assert.Contains("16,530.25", cut.Markup);
            Assert.Contains("16,531.00", cut.Markup);
            Assert.Contains("16,529.50", cut.Markup);
            Assert.Contains("Datastore", cut.Markup);
            Assert.Contains("1 record(s)", cut.Markup);
        });
    }

    [Fact]
    public void History_EmptyResponse_ShowsEmptyState()
    {
        var cut = RenderHistory(_ => JsonResponse("[]"));

        cut.Find(".history-button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No persisted prices found", cut.Markup);
            Assert.Contains("0 record(s)", cut.Markup);
        });
    }

    [Fact]
    public void History_InvalidRange_ShowsValidationErrorWithoutRequest()
    {
        var requests = 0;

        var cut = RenderHistory(_ =>
        {
            requests++;
            return JsonResponse("[]");
        });

        cut.Find("#from-date").Change("2023-01-03");
        cut.Find("#to-date").Change("2023-01-01");

        cut.Find(".history-button").Click();

        Assert.Contains(
            "The start timestamp must not be later than the end timestamp.",
            cut.Markup);

        Assert.Equal(0, requests);
    }

    [Fact]
    public void History_ApiFailure_ShowsBackendMessage()
    {
        var cut = RenderHistory(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """
                    {
                      "title": "Invalid price history request",
                      "status": 400,
                      "errors": {
                        "range": [
                          "The 'from' timestamp must not be later than the 'to' timestamp."
                        ]
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/problem+json")
            });

        cut.Find(".history-button").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "The 'from' timestamp must not be later than the 'to' timestamp.",
                cut.Markup));
    }

    [Fact]
    public async Task History_WhileLoading_ShowsLoadingStateAndDisablesButton()
    {
        var pendingResponse =
            new TaskCompletionSource<HttpResponseMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var httpClient = new HttpClient(
            new AsyncStubHandler((_, _) => pendingResponse.Task))
        {
            BaseAddress = new Uri("https://localhost:7017/")
        };

        Services.AddSingleton(new PriceApiClient(httpClient));

        var cut = Render<History>();

        cut.Find(".history-button").Click();

        cut.WaitForAssertion(() =>
        {
            var button = cut.Find(".history-button");

            Assert.True(button.HasAttribute("disabled"));
            Assert.Contains("Loading history...", button.TextContent);
            Assert.Contains(
                "Loading persisted price history...",
                cut.Markup);
        });

        pendingResponse.SetResult(JsonResponse("[]"));

        cut.WaitForAssertion(() =>
        {
            var button = cut.Find(".history-button");

            Assert.False(button.HasAttribute("disabled"));
            Assert.Contains("Search persisted prices", button.TextContent);
            Assert.Contains("No persisted prices found", cut.Markup);
        });
    }


    [Fact]
    public void History_Search_UsesHistoryEndpointWithUtcRange()
    {
        Uri? requestedUri = null;

        var cut = RenderHistory(request =>
        {
            requestedUri = request.RequestUri;
            return JsonResponse("[]");
        });

        cut.Find(".history-button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(requestedUri);

            Assert.Equal(
                "/api/prices/BTCUSD/history",
                requestedUri.AbsolutePath);

            Assert.Contains(
                "from=2023-01-01T00%3A00%3A00Z",
                requestedUri.Query);

            Assert.Contains(
                "to=2023-01-02T00%3A00%3A00Z",
                requestedUri.Query);
        });
    }


    private IRenderedComponent<History> RenderHistory(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        var httpClient = new HttpClient(
            new StubHandler(responseFactory))
        {
            BaseAddress = new Uri("https://localhost:7017/")
        };

        Services.AddSingleton(new PriceApiClient(httpClient));

        return Render<History>();
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json")
        };
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

    private sealed class AsyncStubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>
            responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return responseFactory(request, cancellationToken);
        }
    }
}
