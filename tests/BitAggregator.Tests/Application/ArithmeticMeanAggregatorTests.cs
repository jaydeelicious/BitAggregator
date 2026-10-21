using BitAggregator.Application.Aggregation;

namespace BitAggregator.Tests.Application;

public sealed class ArithmeticMeanAggregatorTests
{
    private readonly ArithmeticMeanAggregator _aggregator = new();

    [Fact]
    public void Aggregate_ReturnsArithmeticMean()
    {
        double[] prices = [16531.00, 16529.50];

        var result = _aggregator.Aggregate(prices);

        Assert.Equal(16530.25, result);
    }

    [Fact]
    public void Aggregate_ReturnsSinglePrice()
    {
        double[] prices = [16531.00];

        var result = _aggregator.Aggregate(prices);

        Assert.Equal(16531.00, result);
    }

    [Fact]
    public void Aggregate_RejectsEmptyCollection()
    {
        Assert.Throws<ArgumentException>(() => _aggregator.Aggregate(Array.Empty<double>()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Aggregate_RejectsInvalidPrices(double invalidPrice)
    {
        double[] prices = [16531.00, invalidPrice];

        Assert.Throws<ArgumentException>(() =>
            _aggregator.Aggregate(prices));
    }

    [Fact]
    public void Aggregate_RejectsNullCollection()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _aggregator.Aggregate(null!));
    }
}