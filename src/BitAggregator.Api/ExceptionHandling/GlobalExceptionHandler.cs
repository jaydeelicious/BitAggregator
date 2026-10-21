
using System.Net;
using System.Text.Json;
using BitAggregator.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BitAggregator.Api.ExceptionHandling;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // If the client disconnected, don't report an upstream failure.
        if (exception is OperationCanceledException &&
            httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation(
                "Request was cancelled by the client.");

            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.StatusCode = 499;
            }

            return true;
        }

        var isProviderTimeout =
            exception is OperationCanceledException &&
            !httpContext.RequestAborted.IsCancellationRequested;

        var isProviderFailure =
            exception is PriceSourceUnavailableException or
                HttpRequestException or
                JsonException or
                FormatException or
                TimeoutException ||
            isProviderTimeout;

        var statusCode = isProviderFailure
            ? (int)HttpStatusCode.BadGateway
            : StatusCodes.Status500InternalServerError;

        logger.LogError(
            exception,
            "Request failed with status code {StatusCode}",
            statusCode);

        httpContext.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = isProviderFailure
                ? "Price provider unavailable"
                : "An unexpected error occurred",
            Detail = isProviderFailure
                ? "Unable to retrieve a complete price from the configured sources."
                : "An internal server error occurred.",
            Instance = httpContext.Request.Path
        };

        return await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problem
            });
    }
}
