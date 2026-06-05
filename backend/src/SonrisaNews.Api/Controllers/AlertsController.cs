using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Alerts;
using SonrisaNews.Domain.Auth;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Infrastructure.Alerts;

namespace SonrisaNews.Api.Controllers;

/// <summary>
/// User-owned alert CRUD + channel-mode matrix. Alerts are inert until
/// the matcher (wave 6) wires them up; this controller is the API
/// surface for managing them.
/// </summary>
/// <remarks>
/// Every action is guarded by <see cref="Permissions.AlertsReadOwn"/> or
/// <see cref="Permissions.AlertsWriteOwn"/>. Resource ownership is
/// enforced inside the <see cref="IAlertService"/>: a user can only
/// see and edit their own alerts, regardless of the policy
/// permission. The Admin-only <c>Alerts.Read.Any</c> /
/// <c>Alerts.Write.Any</c> permissions exist for the wave 10 admin
/// area, which is out of scope here.
/// </remarks>
[ApiController]
[Route("api/v1/alerts")]
public class AlertsController(IAlertService alertService) : ControllerBase
{
    /// <summary>List the caller's alerts, newest first. Empty for a new user.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.AlertsReadOwn)]
    [ProducesResponseType(typeof(IReadOnlyList<AlertResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AlertResponse>>> ListAsync(CancellationToken ct)
    {
        var result = await alertService.ListAsync(ct);
        if (!result.IsSuccess)
        {
            return MapAuth(result.Outcome);
        }

        return Ok(result.Payload!.Select(AlertResponse.From).ToList());
    }

    /// <summary>Get a single alert by id. Returns 404 if the alert does not exist OR does not belong to the caller (no existence leak).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.AlertsReadOwn)]
    [ProducesResponseType(typeof(AlertResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlertResponse>> GetAsync(Guid id, CancellationToken ct)
    {
        var bad = RejectEmptyGuid(id, nameof(id));
        if (bad is not null) return bad;
        var result = await alertService.GetAsync(id, ct);
        return result.IsSuccess
            ? Ok(AlertResponse.From(result.Payload!))
            : MapNotFound(result.Outcome);
    }

    /// <summary>Create a new alert. The <c>filters</c> JSON is schema-validated per <c>type</c>; unknown fields are rejected.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.AlertsWriteOwn)]
    [ProducesResponseType(typeof(AlertResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AlertResponse>> CreateAsync(
        [FromBody] CreateAlertRequest req,
        CancellationToken ct)
    {
        var result = await alertService.CreateAsync(
            new CreateAlertInput(req.Name, req.Type, req.Filters ?? "{}"),
            ct);

        if (!result.IsSuccess)
        {
            if (result.Outcome == AlertOutcome.InvalidFilter)
            {
                return BuildValidationProblem(result.Errors!);
            }

            return MapAuth(result.Outcome);
        }

        var dto = AlertResponse.From(result.Payload!);
        return CreatedAtAction(nameof(GetAsync), new { id = dto.Id }, dto);
    }

    /// <summary>Update mutable fields on an existing alert. Only the supplied fields are changed.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.AlertsWriteOwn)]
    [ProducesResponseType(typeof(AlertResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlertResponse>> UpdateAsync(
        Guid id,
        [FromBody] UpdateAlertRequest req,
        CancellationToken ct)
    {
        var bad = RejectEmptyGuid(id, nameof(id));
        if (bad is not null) return bad;
        var result = await alertService.UpdateAsync(
            id,
            new UpdateAlertInput(req.Name, req.Filters, req.Enabled),
            ct);

        if (!result.IsSuccess)
        {
            if (result.Outcome == AlertOutcome.InvalidFilter)
            {
                return BuildValidationProblem(result.Errors!);
            }

            return MapNotFound(result.Outcome);
        }

        return Ok(AlertResponse.From(result.Payload!));
    }

    /// <summary>Delete an alert. Cascade removes its channel-mode rows.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.AlertsWriteOwn)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var bad = RejectEmptyGuid(id, nameof(id));
        if (bad is not null) return bad;
        var result = await alertService.DeleteAsync(id, ct);
        return result.IsSuccess
            ? NoContent()
            : MapNotFound(result.Outcome);
    }

    // -- Channel-mode matrix -------------------------------------------------

    /// <summary>List the channel-mode rows wired to this alert. Returns 404 if the alert does not exist or does not belong to the caller.</summary>
    [HttpGet("{alertId:guid}/channels")]
    [Authorize(Policy = Permissions.AlertsReadOwn)]
    [ProducesResponseType(typeof(IReadOnlyList<AlertChannelModeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AlertChannelModeResponse>>> ListChannelModesAsync(
        Guid alertId,
        CancellationToken ct)
    {
        var bad = RejectEmptyGuid(alertId, nameof(alertId));
        if (bad is not null) return bad;
        var result = await alertService.ListChannelModesAsync(alertId, ct);
        return result.IsSuccess
            ? Ok(result.Payload!.Select(AlertChannelModeResponse.From).ToList())
            : MapNotFound(result.Outcome);
    }

    /// <summary>Set (upsert) the delivery mode for one (alert, channel) pair. The channel must also be owned by the caller.</summary>
    [HttpPost("{alertId:guid}/channels")]
    [Authorize(Policy = Permissions.AlertsWriteOwn)]
    [ProducesResponseType(typeof(AlertChannelModeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlertChannelModeResponse>> SetChannelModeAsync(
        Guid alertId,
        [FromBody] SetChannelModeRequest req,
        CancellationToken ct)
    {
        var bad = RejectEmptyGuid(alertId, nameof(alertId));
        if (bad is not null) return bad;
        var result = await alertService.SetChannelModeAsync(
            alertId,
            new SetChannelModeInput(req.ChannelId, req.Mode),
            ct);

        // Both NotFound and ChannelNotFound are 404 with different titles; see MapNotFound.
        return result.IsSuccess
            ? Ok(AlertChannelModeResponse.From(result.Payload!))
            : MapNotFound(result.Outcome);
    }

    /// <summary>Remove a channel-mode row. The channel stops receiving matches from this alert.</summary>
    [HttpDelete("{alertId:guid}/channels/{channelId:guid}")]
    [Authorize(Policy = Permissions.AlertsWriteOwn)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveChannelModeAsync(
        Guid alertId,
        Guid channelId,
        CancellationToken ct)
    {
        var bad = RejectEmptyGuid(alertId, nameof(alertId));
        if (bad is not null) return bad;
        bad = RejectEmptyGuid(channelId, nameof(channelId));
        if (bad is not null) return bad;
        var result = await alertService.RemoveChannelModeAsync(alertId, channelId, ct);
        return result.IsSuccess
            ? NoContent()
            : MapNotFound(result.Outcome);
    }

    // -- Test ----------------------------------------------------------------

    /// <summary>
    /// Re-runs the matcher against the most recent 50 events for this
    /// alert and returns the "would have fired" list. This is a
    /// <b>read-only preview</b> — no <c>Match</c> or <c>Notification</c>
    /// rows are inserted. The frontend's "Test this alert" button
    /// (wave 5 §2.2 / wave 6 scope) calls this endpoint.
    /// </summary>
    [HttpPost("{id:guid}/test")]
    [Authorize(Policy = Permissions.AlertsTestOwn)]
    [ProducesResponseType(typeof(IReadOnlyList<TestAlertHitResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TestAlertHitResponse>>> TestAsync(Guid id, CancellationToken ct)
    {
        var bad = RejectEmptyGuid(id, nameof(id));
        if (bad is not null) return bad;
        var result = await alertService.TestAsync(id, ct);
        if (!result.IsSuccess)
        {
            return MapNotFound(result.Outcome);
        }
        var hits = result.Payload!.Select(TestAlertHitResponse.From).ToList();
        return Ok(hits);
    }

    // -- helpers -------------------------------------------------------------

    /// <summary>
    /// Rejects a route-bound Guid that's <see cref="Guid.Empty"/>. The
    /// <c>{id:guid}</c> route constraint parses <c>00000000-0000-0000-0000-000000000000</c>
    /// as a valid Guid, so the action body would otherwise hit the DB
    /// and return a misleading 404. Returning a 400 with the param
    /// name in the message makes the failure obvious.
    /// </summary>
    private BadRequestObjectResult? RejectEmptyGuid(Guid id, string paramName) =>
        id == Guid.Empty
            ? BadRequest($"{paramName} must be a non-empty Guid.")
            : null;

    private ActionResult MapAuth(AlertOutcome outcome) => outcome switch
    {
        AlertOutcome.Unauthenticated => Unauthorized(),
        _ => Forbid(),
    };

    private ActionResult MapNotFound(
        AlertOutcome outcome,
        [System.Runtime.CompilerServices.CallerMemberName] string? callerAction = null) => outcome switch
    {
        AlertOutcome.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: "Alert not found"),
        AlertOutcome.ChannelNotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: "Channel not found"),
        AlertOutcome.Unauthenticated => Unauthorized(),
        // Fail loudly if a future refactor routes a new outcome through
        // here. The action's name (from CallerMemberName) is included
        // so a production stack trace says "MapNotFound(SetChannelModeAsync)"
        // rather than just "MapNotFound".
        _ => throw new InvalidOperationException(
            $"MapNotFound({callerAction}): unexpected outcome {outcome}. Update the switch or route the outcome to the right mapper."),
    };

    private static Dictionary<string, string[]> MapValidation(IReadOnlyList<AlertFiltersError> errors)
    {
        var grouped = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var err in errors)
        {
            if (!grouped.TryGetValue(err.Field, out var list))
            {
                list = new List<string>();
                grouped[err.Field] = list;
            }
            list.Add(err.Message);
        }
        return grouped.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }

    private ActionResult BuildValidationProblem(IReadOnlyList<AlertFiltersError> errors)
    {
        var problem = new ValidationProblemDetails(MapValidation(errors))
        {
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
        };
        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" },
        };
    }
}

// --- DTOs -------------------------------------------------------------------

/// <summary>Request to create a new alert. The filter JSON shape depends on <see cref="Type"/>.</summary>
public sealed record CreateAlertRequest(
    [Required][StringLength(200, MinimumLength = 1)] string Name,
    [Required] AlertType Type,
    string? Filters);

/// <summary>Request to update mutable fields on an alert. Null fields are not changed.</summary>
public sealed record UpdateAlertRequest(
    [StringLength(200, MinimumLength = 1)] string? Name,
    string? Filters,
    bool? Enabled);

/// <summary>Request to set the delivery mode for an (alert, channel) pair.</summary>
public sealed record SetChannelModeRequest(
    [Required] Guid ChannelId,
    [Required] DeliveryMode Mode);

/// <summary>Response shape for an alert.</summary>
public sealed record AlertResponse(
    Guid Id,
    string Name,
    AlertType Type,
    bool Enabled,
    string Filters,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static AlertResponse From(Alert a) => new(
        a.Id, a.Name, a.Type, a.Enabled, a.Filters, a.CreatedAt, a.UpdatedAt);
}

/// <summary>Response shape for an alert-channel-mode row.</summary>
public sealed record AlertChannelModeResponse(
    Guid AlertId,
    Guid ChannelId,
    DeliveryMode Mode,
    DateTimeOffset CreatedAt)
{
    public static AlertChannelModeResponse From(AlertChannelMode r) => new(
        r.AlertId, r.ChannelId, r.Mode, r.CreatedAt);
}

/// <summary>
/// One row in the "Test this alert" preview. The frontend uses the
/// <c>EventId</c> for deep-linking and the <c>Summary</c> as the
/// user-visible line.
/// </summary>
public sealed record TestAlertHitResponse(Guid EventId, string Summary)
{
    public static TestAlertHitResponse From(SonrisaNews.Infrastructure.Matcher.TestAlertHit hit) =>
        new(hit.EventId, hit.Summary);
}
