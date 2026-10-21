using BitAggregator.Api.Contracts;
using BitAggregator.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BitAggregator.Api.Controllers;

[ApiController]
[Route("api/prices")]
public sealed class PricesController(IPriceAggregationService priceAggregationService) : ControllerBase
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
}