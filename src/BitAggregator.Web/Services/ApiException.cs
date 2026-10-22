using System.Net;

namespace BitAggregator.Web.Services;

public sealed class ApiException(
    HttpStatusCode statusCode,
    string message)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}