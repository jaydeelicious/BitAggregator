namespace BitAggregator.Application.Exceptions;

public sealed class PriceAlreadyExistsException : Exception
{
    public PriceAlreadyExistsException(Exception innerException)
        : base("An aggregated price already exists for this time point.", innerException)
    {
    }
}