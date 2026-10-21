using BitAggregator.Application.Models;

namespace BitAggregator.Application.Interfaces;

public interface IPriceSource
{
    string Name { get; }

    Task<SourcePriceResult?> GetClosePriceAsync(
        string instrument,
        DateTime timestamp,
        CancellationToken cancellationToken = default);
}