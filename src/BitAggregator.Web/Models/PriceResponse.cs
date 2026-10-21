namespace BitAggregator.Web.Models;

public sealed record SourcePriceResponse(
    string Source,
    double Price);

public sealed record PriceResponse(
    string Instrument,
    DateTime Timestamp,
    double AggregatedPrice,
    string ResultSource,
    IReadOnlyList<SourcePriceResponse> SourcePrices);