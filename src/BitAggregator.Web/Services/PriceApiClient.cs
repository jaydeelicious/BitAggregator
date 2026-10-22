
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BitAggregator.Web.Models;

namespace BitAggregator.Web.Services;

public sealed class PriceApiClient(HttpClient httpClient)
{
    public async Task<PriceResponse> GetPriceAsync(
        string instrument,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(
            $"api/prices/{Uri.EscapeDataString(instrument)}",
            ("timestamp", timestamp));

        return await GetAsync<PriceResponse>(
            url, cancellationToken);
    }

    public async Task<IReadOnlyList<PriceResponse>> GetHistoryAsync(
        string instrument,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(
            $"api/prices/{Uri.EscapeDataString(instrument)}/history",
            ("from", from),
            ("to", to));

        return await GetAsync<PriceResponse[]>(
            url, cancellationToken);
    }

    private async Task<T> GetAsync<T>(
        string url,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(
                response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<T>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                "The API returned an empty response.");
    }

    private static async Task<ApiException> CreateApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string? message = null;

        try
        {
            using var body = await response.Content.ReadFromJsonAsync<
                JsonDocument>(
                cancellationToken: cancellationToken);

            if (body is not null)
            {
                var root = body.RootElement;

                if (root.TryGetProperty("errors", out var errors) &&
                    errors.ValueKind == JsonValueKind.Object)
                {
                    var messages = errors
                        .EnumerateObject()
                        .SelectMany(property =>
                            property.Value.ValueKind == JsonValueKind.Array
                                ? property.Value.EnumerateArray()
                                    .Where(item =>
                                        item.ValueKind == JsonValueKind.String)
                                    .Select(item => item.GetString())
                                : Enumerable.Empty<string?>())
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToArray();

                    if (messages.Length > 0)
                    {
                        message = string.Join(" ", messages);
                    }
                }

                if (string.IsNullOrWhiteSpace(message) &&
                    root.TryGetProperty("detail", out var detail) &&
                    detail.ValueKind == JsonValueKind.String)
                {
                    message = detail.GetString();
                }

                if (string.IsNullOrWhiteSpace(message) &&
                    root.TryGetProperty("title", out var title) &&
                    title.ValueKind == JsonValueKind.String)
                {
                    message = title.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // Non-JSON error responses will use the fallback message.
        }
        catch (HttpRequestException)
        {
            // An unreadable error body will use the fallback message.
        }
        catch (NotSupportedException)
        {
            // An unsupported response content type will use the fallback.
        }

        message ??= response.StatusCode switch
        {
            HttpStatusCode.BadRequest =>
                "The request contains invalid values.",
            HttpStatusCode.BadGateway =>
                "The price providers are currently unavailable.",
            HttpStatusCode.ServiceUnavailable =>
                "The service is temporarily unavailable.",
            _ =>
                $"The request failed (HTTP {(int)response.StatusCode})."
        };

        return new ApiException(response.StatusCode, message);
    }

    private static string BuildUrl(
        string path,
        params (string Name, DateTimeOffset Value)[] parameters)
    {
        var query = string.Join("&", parameters.Select(parameter =>
        {
            var value = parameter.Value.ToUniversalTime()
                .ToString(
                    "yyyy-MM-dd'T'HH:mm:ss'Z'",
                    CultureInfo.InvariantCulture);

            return $"{parameter.Name}={Uri.EscapeDataString(value)}";
        }));

        return $"{path}?{query}";
    }
}
