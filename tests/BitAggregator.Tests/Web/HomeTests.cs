
using System.Net;
using System.Text;
using BitAggregator.Web.Pages;
using BitAggregator.Web.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BitAggregator.Tests.Web;

public sealed class HomeTests : BunitContext
{
    [Fact]
    public void Home_RendersLookupControls()
    {
        var cut = RenderHome(_ => JsonResponse(
            PriceJson("ExternalApis")));

        Assert.NotNull(cut.Find("#instrument"));
        Assert.NotNull(cut.Find("#price-date"));
        Assert.NotNull(cut.Find("#price-hour"));
        Assert.Equal(24, cut.FindAll("#price-hour option").Count);
        Assert.Contains("Get aggregated price", cut.Markup);
    }

    [Fact]
    public void Lookup_Success_DisplaysPriceAndSources()
    {
        var cut = RenderHome(_ => JsonResponse(
            PriceJson("ExternalApis")));

        cut.Find(".lookup-button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("16,530.25", cut.Markup);
            Assert.Contains("Bitstamp", cut.Markup);
            Assert.Contains("Bitfinex", cut.Markup);
            Assert.Contains("External APIs", cut.Markup);
        });
    }

    [Fact]
    public void Lookup_FutureDate_ShowsValidationError()
    {
        var requests = 0;

        var cut = RenderHome(_ =>
        {
            requests++;
            return JsonResponse(PriceJson("ExternalApis"));
        });

        cut.Find("#price-date").Change("2099-01-01");
        cut.Find(".lookup-button").Click();

        Assert.Contains(
            "The requested hourly candle is not yet complete.",
            cut.Markup);

        Assert.Equal(0, requests);
    }

    [Fact]
    public void Lookup_BackendFailure_ShowsErrorMessage()
    {
        var cut = RenderHome(_ => new HttpResponseMessage(
            HttpStatusCode.BadGateway)
        {
            Content = new StringContent(
                """
                {
                  "title": "Price providers unavailable",
                  "status": 502,
                  "detail": "Unable to retrieve prices from exchanges."
                }
                """,
                Encoding.UTF8,
                "application/problem+json")
        });

        cut.Find(".lookup-button").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "Unable to retrieve prices from exchanges.",
                cut.Markup));
    }

    [Fact]
    public void Lookup_RepeatedRequest_DisplaysDatastoreOrigin()
    {
        var requests = 0;

        var cut = RenderHome(_ =>
        {
            requests++;

            return JsonResponse(PriceJson(
                requests == 1
                    ? "ExternalApis"
                    : "Datastore"));
        });

        cut.Find(".lookup-button").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("External APIs", cut.Markup));

        cut.Find(".lookup-button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Datastore", cut.Markup);
            Assert.Contains(
                "Retrieved from persisted prices",
                cut.Markup);
            Assert.Equal(2, requests);
        });
    }


    [Fact]
    public async Task Lookup_WhileLoading_DisablesButtonAndShowsLoadingState()
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

        var cut = Render<Home>();

        cut.Find(".lookup-button").Click();

        cut.WaitForAssertion(() =>
        {
            var button = cut.Find(".lookup-button");

            Assert.True(button.HasAttribute("disabled"));
            Assert.Contains("Retrieving price...", button.TextContent);
            Assert.Contains(
                "Retrieving the aggregated price...",
                cut.Markup);
        });

        pendingResponse.SetResult(JsonResponse(
            PriceJson("ExternalApis")));

        cut.WaitForAssertion(() =>
        {
            var button = cut.Find(".lookup-button");

            Assert.False(button.HasAttribute("disabled"));
            Assert.Contains("Get aggregated price", button.TextContent);
            Assert.Contains("16,530.25", cut.Markup);
        });
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

    private IRenderedComponent<Home> RenderHome(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        var httpClient = new HttpClient(
            new StubHandler(responseFactory))
        {
            BaseAddress = new Uri("https://localhost:7017/")
        };

        Services.AddSingleton(new PriceApiClient(httpClient));

        return Render<Home>();
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

    private static string PriceJson(string resultSource) =>
        $$"""
        {
          "instrument": "BTC/USD",
          "timestamp": "2023-01-01T01:00:00Z",
          "aggregatedPrice": 16530.25,
          "resultSource": "{{resultSource}}",
          "sourcePrices": [
            { "source": "Bitstamp", "price": 16531.0 },
            { "source": "Bitfinex", "price": 16529.5 }
          ]
        }
        """;

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
