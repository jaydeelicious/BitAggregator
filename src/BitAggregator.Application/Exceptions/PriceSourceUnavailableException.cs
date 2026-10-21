
namespace BitAggregator.Application.Exceptions;

public sealed class PriceSourceUnavailableException : Exception
{
    public PriceSourceUnavailableException(string message)
        : base(message)
    {
    }

    public PriceSourceUnavailableException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
