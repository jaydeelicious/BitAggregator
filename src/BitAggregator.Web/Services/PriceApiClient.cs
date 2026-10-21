using System.Globalization;
using System.Net.Http.Json;
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

        return await httpClient.GetFromJsonAsync<PriceResponse>(
            url, cancellationToken)
            ?? throw new InvalidOperationException("The price API returned an empty response.");
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

        return await httpClient.GetFromJsonAsync<PriceResponse[]>(
            url,
            cancellationToken)
            ?? throw new InvalidOperationException("The price history API returned an empty response");
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