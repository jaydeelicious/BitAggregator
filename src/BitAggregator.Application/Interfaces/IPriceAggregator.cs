namespace BitAggregator.Application.Interfaces;

public interface IPriceAggregator
{
    double Aggregate(IReadOnlyCollection<double> prices);
}