using BitAggregator.Application.Interfaces;
using BitAggregator.Domain.Entities;
using BitAggregator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BitAggregator.Infrastructure.Repositories;

public sealed class AggregatedPriceRepository(BitAggregatorDbContext dbContext) : IAggregatedPriceRepository
{
    public async Task<AggregatedPrice?> GetByInstrumentAndTimestampAsync(
        string instrument,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.AggregatedPrices
            .AsNoTracking()
            .Include(x => x.SourcePrices)
            .SingleOrDefaultAsync(
                x => x.Instrument == instrument &&
                    x.Timestamp == timestamp,
                cancellationToken);
    }

    public async Task<IReadOnlyList<AggregatedPrice>> GetByTimeRangeAsync(
        string instrument,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.AggregatedPrices
            .AsNoTracking()
            .Include(x => x.SourcePrices)
            .Where(x =>
                x.Instrument == instrument &&
                x.Timestamp >= from &&
                x.Timestamp <= to)
            .OrderBy(x => x.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        AggregatedPrice aggregatedPrice,
        CancellationToken cancellationToken = default)
    {
        await dbContext.AggregatedPrices.AddAsync(
            aggregatedPrice,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}