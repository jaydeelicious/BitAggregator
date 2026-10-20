using Microsoft.AspNetCore.Mvc;

namespace BitAggregator.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public ActionResult Get()
    {
        return Ok(new
        {
            status = "Healthy"
        });
    }
}