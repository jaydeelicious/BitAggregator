namespace BitAggregator.Domain.Entities;

public sealed class AggregatedPrice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Instrument { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    public double Price { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SourcePrice> SourcePrices { get; set; } = new List<SourcePrice>();
}