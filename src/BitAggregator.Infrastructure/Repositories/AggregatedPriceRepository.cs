using BitAggregator.Application.Interfaces;
using BitAggregator.Domain.Entities;
using BitAggregator.Infrastructure.Persistence;
using BitAggregator.Application.Exceptions;
using Microsoft.Data.Sqlite;
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

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqliteException
            {
                SqliteErrorCode: 19,
                SqliteExtendedErrorCode: 2067
            } sqliteException &&
            sqliteException.Message.Contains("AggregatedPrices.Instrument, AggregatedPrices.Timestamp", StringComparison.Ordinal))
        {
            dbContext.ChangeTracker.Clear();

            throw new PriceAlreadyExistsException(ex);
        }
    }
}