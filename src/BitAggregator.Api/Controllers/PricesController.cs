using BitAggregator.Api.Contracts;
using BitAggregator.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BitAggregator.Api.Controllers;

[ApiController]
[Route("api/prices")]
public sealed class PricesController(
    IPriceAggregationService priceAggregationService,
    IAggregatedPriceRepository priceRepository) 
    : ControllerBase
{
    [HttpGet("{instrument}")]
    [ProducesResponseType(typeof(PriceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PriceResponse>> GetPrice(
        string instrument,
        [FromQuery] DateTimeOffset? timestamp,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (!string.Equals(
                instrument,
                "BTCUSD",
                StringComparison.OrdinalIgnoreCase))
        {
            errors["instrument"] =
            [
                "Only BTCUSD is currently supported."
            ];
        }

        if (timestamp is null)
        {
            errors["timestamp"] =
            [
                "A valid UTC timestamp is required."
            ];
        }
        else
        {
            var requestedTime = timestamp.Value;

            if (requestedTime.Offset != TimeSpan.Zero)
            {
                errors["timestamp"] =
                [
                    "Timestamp must use UTC (Z or +00:00)."
                ];
            }
            else if (requestedTime.UtcDateTime.Ticks % TimeSpan.TicksPerHour != 0)
            {
                errors["timestamp"] =
                [
                    "Timestamp must be aligned to a full hour."
                ];
            }
            else if (requestedTime.UtcDateTime > DateTime.UtcNow)
            {
                errors["timestamp"] =
                [
                    "Future timestamps are not allowed."
                ];
            }
        }

        if (errors.Count > 0)
        {
            return ValidationProblem(
                new ValidationProblemDetails(errors)
                {
                    Title = "Invalid price lookup request",
                    Status = StatusCodes.Status400BadRequest
                });
        }

        var result = await priceAggregationService.GetPriceAsync(
            "BTC/USD",
            timestamp!.Value.UtcDateTime,
            cancellationToken);

        var response = new PriceResponse(
            result.Instrument,
            result.Timestamp,
            result.AggregatedPrice,
            result.Origin.ToString(),
            result.SourcePrices
                .Select(x => new SourcePriceResponse(
                    x.SourceName,
                    x.Price))
                .ToArray());

        return Ok(response);
    }

    [HttpGet("{instrument}/history")]
    [ProducesResponseType(
        typeof(IReadOnlyList<PriceResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ValidationProblemDetails),
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PriceResponse>>> GetHistory(
        string instrument,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (!string.Equals(instrument, "BTCUSD", StringComparison.OrdinalIgnoreCase))
        {
            errors["instrument"] =
            [
                "Only BTCUSD is currently supported."
            ];
        }

        if (from is null || from.Value.Offset != TimeSpan.Zero)
        {
            errors["from"] =
            [
                "A valid UTC 'from' timestamp is required."
            ];
        }

        if (to is null || to.Value.Offset != TimeSpan.Zero)
        {
            errors["to"] =
            [
                "A valid UTC 'to' timestamp is required."
            ];
        }

        if (from is not null && 
            from.Value.Offset == TimeSpan.Zero &&
            from.Value.UtcDateTime.Ticks % TimeSpan.TicksPerHour != 0)
        {
            errors["from"] =
            [
                "The 'from' timestamp must be hour-aligned."
            ];
        }

        if (to is not null &&
            to.Value.Offset == TimeSpan.Zero &&
            to.Value.UtcDateTime.Ticks % TimeSpan.TicksPerHour != 0)
        {
            errors["to"] =
            [
                "The 'to' timestamp must be hour-aligned."
            ];
        }

        if (from.HasValue && 
            to.HasValue &&
            from.Value > to.Value)
        {
            errors["range"] =
            [
                "The 'from' timestamp must not be later than the 'to' timestamp."
            ];
        }

        if (errors.Count > 0)
        {
            return ValidationProblem(
                new ValidationProblemDetails(errors)
                {
                    Title = "Invalid price history request",
                    Status = StatusCodes.Status400BadRequest
                });
        }

        var prices = await priceRepository.GetByTimeRangeAsync(
            "BTC/USD",
            from!.Value.UtcDateTime,
            to!.Value.UtcDateTime,
            cancellationToken);

        var response = prices
            .OrderBy(x => x.Timestamp)
            .Select(x => new PriceResponse(
                x.Instrument,
                x.Timestamp,
                x.Price,
                "Datastore",
                x.SourcePrices
                    .Select(source => new SourcePriceResponse(
                        source.SourceName,
                        source.Price))
                    .ToArray()))
                .ToArray();

        return Ok(response);
    }
}