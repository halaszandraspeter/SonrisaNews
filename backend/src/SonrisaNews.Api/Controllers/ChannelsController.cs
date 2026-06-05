using System.ComponentModel.DataAnnotations;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Auth;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Domain.Notifications;
using SonrisaNews.Infrastructure.Auth;
using SonrisaNews.Infrastructure.Persistence;

namespace SonrisaNews.Api.Controllers;

/// <summary>Notification channel management. Users create, verify, and delete channels where alerts are routed.</summary>
[ApiController]
[Route("api/v1/channels")]
[Authorize(Policy = Permissions.ChannelsWriteOwn)]
public class ChannelsController(
    ICurrentUser currentUser,
    SonrisaNewsDbContext db,
    IServiceProvider serviceProvider) : ControllerBase
{
    /// <summary>Create a new channel. The destination must be verified before any alerts are sent to it.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChannelResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ChannelResponse>> CreateAsync(
        CreateChannelRequest req,
        CancellationToken ct)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        // Resolve the channel implementation
        if (!Enum.TryParse<ChannelType>(req.Type, true, out var channelType))
        {
            return BadRequest(new { error = "invalid_type", message = $"Unknown channel type: {req.Type}" });
        }

        var typeString = channelType switch
        {
            ChannelType.Email => ChannelTypeConstants.Email,
            ChannelType.Slack => ChannelTypeConstants.Slack,
            _ => null
        };

        if (typeString is null)
        {
            return BadRequest(new { error = "unsupported_type", message = $"Channel type {req.Type} is not supported." });
        }

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = channelType,
            Destination = req.Destination,
            Verified = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Channels.Add(channel);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetAsync), new { id = channel.Id }, new ChannelResponse(
            channel.Id,
            channel.Type.ToString(),
            channel.Destination,
            channel.Verified,
            channel.CreatedAt));
    }

    /// <summary>Start verification of a channel. Returns a challenge the user must confirm to verify the channel.</summary>
    [HttpPost("{id:guid}/verify/start")]
    [ProducesResponseType(typeof(VerifyStartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VerifyStartResponse>> StartVerifyAsync(
        Guid id,
        CancellationToken ct)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
        if (channel is null)
        {
            return NotFound();
        }

        // Resolve the channel implementation by type
        var typeString = channel.Type switch
        {
            ChannelType.Email => ChannelTypeConstants.Email,
            ChannelType.Slack => ChannelTypeConstants.Slack,
            _ => null
        };

        if (typeString is null)
        {
            return BadRequest(new { error = "unsupported_type", message = "This channel type is no longer supported." });
        }

        var impl = serviceProvider.GetKeyedService<INotificationChannel>(typeString);
        if (impl is null)
        {
            return BadRequest(new { error = "implementation_missing", message = "No implementation found for this channel type." });
        }

        var challenge = await impl.StartVerificationAsync(channel.Destination, ct);
        return Ok(new VerifyStartResponse(challenge.Code));
    }

    /// <summary>Confirm verification of a channel by providing the code from the verification challenge.</summary>
    [HttpPost("{id:guid}/verify/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmVerifyAsync(
        Guid id,
        VerifyConfirmRequest req,
        CancellationToken ct)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
        if (channel is null)
        {
            return NotFound();
        }

        // Resolve the channel implementation by type
        var typeString = channel.Type switch
        {
            ChannelType.Email => ChannelTypeConstants.Email,
            ChannelType.Slack => ChannelTypeConstants.Slack,
            _ => null
        };

        if (typeString is null)
        {
            return BadRequest(new { error = "unsupported_type", message = "This channel type is no longer supported." });
        }

        var impl = serviceProvider.GetKeyedService<INotificationChannel>(typeString);
        if (impl is null)
        {
            return BadRequest(new { error = "implementation_missing", message = "No implementation found for this channel type." });
        }

        var isValid = await impl.VerifyAsync(channel.Destination, req.Code, ct);
        if (!isValid)
        {
            return BadRequest(new { error = "verification_failed", message = "The verification code is incorrect." });
        }

        channel.Verified = true;
        db.Channels.Update(channel);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>Delete a channel. No further alerts will be sent to this channel.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(
        Guid id,
        CancellationToken ct)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
        if (channel is null)
        {
            return NotFound();
        }

        db.Channels.Remove(channel);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>Get a channel by id. Ownership is enforced.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ChannelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChannelResponse>> GetAsync(
        Guid id,
        CancellationToken ct)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
        if (channel is null)
        {
            return NotFound();
        }

        return Ok(new ChannelResponse(
            channel.Id,
            channel.Type.ToString(),
            channel.Destination,
            channel.Verified,
            channel.CreatedAt));
    }
}

/// <summary>Request to create a channel.</summary>
public sealed record CreateChannelRequest(
    [Required][StringLength(50)] string Type,
    [Required][StringLength(500)] string Destination);

/// <summary>Response shape for a channel.</summary>
public sealed record ChannelResponse(
    Guid Id,
    string Type,
    string Destination,
    bool Verified,
    DateTimeOffset CreatedAt);

/// <summary>Response shape for starting channel verification.</summary>
public sealed record VerifyStartResponse(string Code);

/// <summary>Request to confirm channel verification.</summary>
public sealed record VerifyConfirmRequest(
    [Required][StringLength(100)] string Code);
