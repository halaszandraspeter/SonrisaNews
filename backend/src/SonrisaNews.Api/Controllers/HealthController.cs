using Microsoft.AspNetCore.Mvc;
using SonrisaNews.Shared;

namespace SonrisaNews.Api.Controllers;

/// <summary>Health endpoints for the orchestrator / load balancer.</summary>
[ApiController]
[Route("api/v1/health")]
public class HealthController(IClock clock) : ControllerBase
{
    /// <summary>Simple liveness probe — does not check downstream services.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() => Ok(HealthResponse.Alive(clock.UtcNow));
}

/// <summary>Response shape for <see cref="HealthController.Get"/>.</summary>
public sealed record HealthResponse(string Status, DateTimeOffset CheckedAt)
{
    public static HealthResponse Alive(DateTimeOffset checkedAt) => new("ok", checkedAt);
}