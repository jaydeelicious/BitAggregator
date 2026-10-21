using System.Text.Json;
using BitAggregator.Application.Interfaces;
using BitAggregator.Application.Models;

namespace BitAggregator.Infrastructure.PriceSources;

public sealed class BitfinexPriceSource(HttpClient httpClient) : IPriceSource
{
    public string Name => "Bitfinex";

    public async Task<SourcePriceResult?> GetClosePriceAsync(
        string instrument,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(instrument, "BTC/USD", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Bitfinex currently supports only BTC/USD.", nameof(instrument));
        }

        if (timestamp.Kind != DateTimeKind.Utc ||
            timestamp.Ticks % TimeSpan.TicksPerHour != 0)
        {
            throw new ArgumentException("Timestamp must be UTC and aligned to a full hour.", nameof(timestamp));
        }

        var candleStart = timestamp.AddHours(-1);

        var startUnix = new DateTimeOffset(candleStart).ToUnixTimeMilliseconds();

        // Inclusive end boundary: only the required candle.
        var endUnix = startUnix + 3_600_000 - 1;

        var requestUri = "v2/candles/trade:1h:tBTCUSD/hist" + $"?start={startUnix}&end={endUnix}&limit=1";

        using var response = await httpClient.GetAsync(requestUri, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Bitfinex returned an invalid candle response.");
        }

        if (root.GetArrayLength() == 0)
        {
            return null;
        }

        var candle = root[0];

        if (candle.ValueKind != JsonValueKind.Array ||
            candle.GetArrayLength() != 6)
        {
            throw new FormatException("Bitfinex returned a malformed candle.");
        }

        var timestampElement = candle[0];
        var closeElement = candle[2];

        if (timestampElement.ValueKind != JsonValueKind.Number ||
            !timestampElement.TryGetInt64(out var actualTimestamp) ||
            closeElement.ValueKind != JsonValueKind.Number ||
            !closeElement.TryGetDouble(out var closePrice))
        {
            throw new FormatException("Bitfinex returned invalid candle values.");
        }

        if (actualTimestamp != startUnix)
        {
            return null;
        }

        if (!double.IsFinite(closePrice) || closePrice <= 0)
        {
            throw new FormatException("Bitfinex returned an invalid closing price.");
        }

        return new SourcePriceResult(Name, closePrice);
    }
}