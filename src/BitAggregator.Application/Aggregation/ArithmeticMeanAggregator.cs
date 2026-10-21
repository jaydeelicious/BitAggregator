using BitAggregator.Application.Interfaces;

namespace BitAggregator.Application.Aggregation;

public sealed class ArithmeticMeanAggregator : IPriceAggregator
{
    public double Aggregate(IReadOnlyCollection<double> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);

        if (prices.Count == 0)
        {
            throw new ArgumentException("At least one price is required.", nameof(prices));
        }

        if (prices.Any(price => !double.IsFinite(price) || price <= 0))
        {
            throw new ArgumentException("All prices must be positive, finite numbers.", nameof(prices));
        }

        return prices.Average();
    }
}