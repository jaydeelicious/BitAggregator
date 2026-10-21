namespace BitAggregator.Application.Models;

public sealed record AggregatedPriceResult(
    string Instrument,
    DateTime Timestamp,
    double AggregatedPrice,
    PriceResultOrigin Origin,
    IReadOnlyList<SourcePriceResult> SourcePrices);