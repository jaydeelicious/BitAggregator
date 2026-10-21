using BitAggregator.Domain.Entities;

namespace BitAggregator.Application.Interfaces;

public interface IAggregatedPriceRepository
{
    Task<AggregatedPrice?> GetByInstrumentAndTimestampAsync(
        string instrument,
        DateTime timestamp,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AggregatedPrice>> GetByTimeRangeAsync(
        string instrument,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        AggregatedPrice aggregatedPrice,
        CancellationToken cancellationToken = default);
}