namespace BitAggregator.Domain.Entities;

public sealed class SourcePrice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AggregatedPriceId { get; set; }

    public string SourceName { get; set; } = string.Empty;

    public double Price { get; set; }

    public AggregatedPrice AggregatedPrice { get; set; } = null!;
}