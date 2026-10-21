using BitAggregator.Application.Models;

namespace BitAggregator.Application.Interfaces;

public interface IPriceAggregationService
{
    Task<AggregatedPriceResult> GetPriceAsync(
        string instrument,
        DateTime timestamp,
        CancellationToken cancellationToken = default);
}