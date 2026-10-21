using BitAggregator.Application.Interfaces;
using BitAggregator.Application.Models;
using BitAggregator.Domain.Entities;
using BitAggregator.Application.Exceptions;

namespace BitAggregator.Application.Services;

public sealed class PriceAggregationService(
    IAggregatedPriceRepository repository,
    IEnumerable<IPriceSource> priceSources,
    IPriceAggregator aggregator) : IPriceAggregationService
{
    private readonly IPriceSource[] _priceSources = priceSources.ToArray();

    public async Task<AggregatedPriceResult> GetPriceAsync(
        string instrument,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetByInstrumentAndTimestampAsync(instrument, timestamp, cancellationToken);

        if (existing is not null)
        {
            return MapResult(existing, PriceResultOrigin.Datastore);
        }

        if (_priceSources.Length == 0)
        {
            throw new InvalidOperationException("No price sources are configured.");
        }

        var tasks = _priceSources.Select(source => source.GetClosePriceAsync(instrument, timestamp, cancellationToken));

        var prices = await Task.WhenAll(tasks);

        if (prices.Any(price => price is null))
        {
            throw new PriceSourceUnavailableException("One or more price sources did not return a price.");
        }

        var sourceResults = prices.Select(price => price!).ToArray();

        var aggregatedPrice = aggregator.Aggregate(sourceResults.Select(x => x.Price).ToArray());

        var entity = new AggregatedPrice
        {
            Instrument = instrument,
            Timestamp = timestamp,
            Price = aggregatedPrice,
            SourcePrices = sourceResults
                .Select(source => new SourcePrice
                {
                    SourceName = source.SourceName,
                    Price = source.Price
                }).ToList()
        };

        try
        {
            await repository.AddAsync(entity, cancellationToken);
        }
        catch (PriceAlreadyExistsException)
        {
            var saved = await repository.GetByInstrumentAndTimestampAsync(instrument, timestamp, cancellationToken);

            if (saved is not null)
            {
                return MapResult(saved, PriceResultOrigin.Datastore);
            }

            throw;
        }

        return MapResult(entity, PriceResultOrigin.ExternalApis);
    }

    private static AggregatedPriceResult MapResult(
        AggregatedPrice entity,
        PriceResultOrigin origin)
    {
        return new AggregatedPriceResult(
            entity.Instrument,
            entity.Timestamp,
            entity.Price,
            origin,
            entity.SourcePrices
                .Select(x => new SourcePriceResult(
                        x.SourceName,
                        x.Price))
                .OrderBy(x => x.SourceName)
                .ToArray());
    }
}