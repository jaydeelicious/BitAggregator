namespace BitAggregator.Application.Models;

public sealed record SourcePriceResult(
    string SourceName,
    double Price);