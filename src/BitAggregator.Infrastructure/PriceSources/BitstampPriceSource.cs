using System.Globalization;
using System.Text.Json;
using BitAggregator.Application.Interfaces;
using BitAggregator.Application.Models;

namespace BitAggregator.Infrastructure.PriceSources;

public sealed class BitstampPriceSource(HttpClient httpClient) : IPriceSource
{
    public string Name => "Bitstamp";

    public async Task<SourcePriceResult?> GetClosePriceAsync(
        string instrument,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(
                instrument,
                "BTC/USD",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Bitstamp currently supports only BTC/USD.",
                nameof(instrument));
        }

        if (timestamp.Kind != DateTimeKind.Utc ||
            timestamp.Minute != 0 ||
            timestamp.Second != 0 ||
            timestamp.Millisecond != 0 ||
            timestamp.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            throw new ArgumentException(
                "Timestamp must be UTC and aligned to a full hour.",
                nameof(timestamp));
        }

        var candleStart = timestamp.AddHours(-1);

        var startUnix = new DateTimeOffset(candleStart).ToUnixTimeSeconds();

        var endUnix = new DateTimeOffset(timestamp).ToUnixTimeSeconds();

        var requestUri = $"api/v2/ohlc/btcusd/?step=3600&limit=1&start={startUnix}";

        using var response = await httpClient.GetAsync(requestUri, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = document.RootElement;

        if (!root.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("ohlc", out var candles) ||
            candles.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Bitstamp returned an invalid OHLC response.");
        }

        if (candles.GetArrayLength() == 0)
        {
            return null;
        }

        var candle = candles[0];

        if (candle.ValueKind != JsonValueKind.Object ||
            !candle.TryGetProperty("timestamp", out var timestampElement) ||
            !candle.TryGetProperty("close", out var closeElement) ||
            !long.TryParse(
                timestampElement.ToString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var actualTimestamp) ||
            !double.TryParse(
                closeElement.ToString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var closePrice))
        {
            throw new FormatException("Bitstamp returned malformed candle data.");
        }

        if (actualTimestamp != startUnix)
        {
            return null;
        }

        if (!double.IsFinite(closePrice) || closePrice <= 0)
        {
            throw new FormatException("Bitstamp returned an invalid closing price.");
        }

        return new SourcePriceResult(Name, closePrice);
    }
}