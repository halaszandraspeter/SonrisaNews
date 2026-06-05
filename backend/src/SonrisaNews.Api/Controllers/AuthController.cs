using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SonrisaNews.Domain.Auth;
using SonrisaNews.Infrastructure.Auth;

namespace SonrisaNews.Api.Controllers;

/// <summary>
/// Authentication endpoints. Sign-up, sign-in, refresh, sign-out,
/// email-verification, forgot-password, reset-password. <see cref="Me"/>
/// is the simplest way for a freshly-issued JWT to prove itself — it
/// returns the current user's profile and is guarded by
/// <see cref="Permissions.ProfileRead"/>.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Creates a new user, sends a verification email, and returns access + refresh tokens.</summary>
    [HttpPost("signup")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SignUpResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SignUpResponse>> SignUp([FromBody] SignUpDto body, CancellationToken ct)
    {
        if (body is null) return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Missing body.");
        var result = await authService.SignUpAsync(
            new SignUpRequest(body.Email, body.Password, body.DisplayName, body.TimeZone),
            ct);

        if (!result.IsSuccess)
        {
            return result.Outcome switch
            {
                AuthOutcome.InvalidInput => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid input", detail: result.ErrorDetail),
                AuthOutcome.EmailAlreadyExists => Problem(statusCode: StatusCodes.Status409Conflict, title: "Email already exists", detail: result.ErrorDetail),
                _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Sign-up failed", detail: result.ErrorDetail),
            };
        }

        SetRefreshCookie(result.Payload!.RefreshToken, result.Payload.RefreshTokenExpiresAt);
        return Created($"/api/v1/auth/me", new SignUpResponse(
            result.User!.Id,
            result.User.Email,
            result.User.DisplayName,
            result.User.Status.ToString(),
            result.Payload.AccessToken,
            result.Payload.AccessTokenExpiresAt));
    }

    /// <summary>Verifies an email address using a one-time token. Idempotent on already-verified emails.</summary>
    [HttpPost("verify")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Verify([FromBody] VerifyDto body, CancellationToken ct)
    {
        var outcome = await authService.VerifyEmailAsync(body.Token, ct);
        return outcome switch
        {
            AuthOutcome.Success => NoContent(),
            AuthOutcome.TokenExpired => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Token expired"),
            AuthOutcome.InvalidToken => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid token"),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Verification failed"),
        };
    }

    /// <summary>Exchanges an email + password for access + refresh tokens.</summary>
    [HttpPost("signin")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SignInResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SignInResponse>> SignIn([FromBody] SignInDto body, CancellationToken ct)
    {
        if (body is null) return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Missing body.");
        var result = await authService.SignInAsync(new SignInRequest(body.Email, body.Password), ct);

        if (!result.IsSuccess)
        {
            return result.Outcome switch
            {
                AuthOutcome.InvalidCredentials => Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials"),
                AuthOutcome.AccountSuspended => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Account unavailable", detail: result.ErrorDetail),
                _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Sign-in failed"),
            };
        }

        SetRefreshCookie(result.Payload!.RefreshToken, result.Payload.RefreshTokenExpiresAt);
        return Ok(new SignInResponse(
            result.User!.Id,
            result.User.Email,
            result.User.DisplayName,
            result.User.Status.ToString(),
            result.Payload.AccessToken,
            result.Payload.AccessTokenExpiresAt));
    }

    /// <summary>Rotates the refresh token and issues a new access token. The old refresh token is revoked.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SignInResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SignInResponse>> Refresh(CancellationToken ct)
    {
        var presented = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(presented))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "No refresh cookie");
        }

        var result = await authService.RefreshAsync(presented, ct);
        if (!result.IsSuccess)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Refresh failed");
        }

        SetRefreshCookie(result.Payload!.RefreshToken, result.Payload.RefreshTokenExpiresAt);
        return Ok(new SignInResponse(
            result.User!.Id,
            result.User.Email,
            result.User.DisplayName,
            result.User.Status.ToString(),
            result.Payload.AccessToken,
            result.Payload.AccessTokenExpiresAt));
    }

    /// <summary>Revokes the current refresh token. Idempotent.</summary>
    [HttpPost("signout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SignOut(CancellationToken ct)
    {
        var presented = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(presented))
        {
            await authService.SignOutAsync(presented, ct);
        }
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth",
        });
        return NoContent();
    }

    /// <summary>Requests a password-reset email. Always returns 200 (no user enumeration).</summary>
    [HttpPost("forgot")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Forgot([FromBody] ForgotDto body, CancellationToken ct)
    {
        await authService.RequestPasswordResetAsync(body.Email, ct);
        return NoContent();
    }

    /// <summary>Sets a new password using a one-time token from the forgot-password email.</summary>
    [HttpPost("reset")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reset([FromBody] ResetDto body, CancellationToken ct)
    {
        var outcome = await authService.ResetPasswordAsync(body.Token, body.NewPassword, ct);
        return outcome switch
        {
            AuthOutcome.Success => NoContent(),
            AuthOutcome.InvalidInput => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid password"),
            AuthOutcome.TokenExpired => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Token expired"),
            AuthOutcome.InvalidToken => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid token"),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Reset failed"),
        };
    }

    private void SetRefreshCookie(string token, DateTimeOffset expiresAt)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,                       // HTTPS-only; the dev server runs on http so this needs a flip — handled by UseCookiePolicy if needed.
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth",
            Expires = expiresAt.UtcDateTime,
        };
        Response.Cookies.Append(RefreshCookieName, token, options);
    }

    /// <summary>Name of the httpOnly refresh-token cookie. The frontend never reads it; the API sets + clears it.</summary>
    public const string RefreshCookieName = "sonrisa_refresh";
}

// --- DTOs -------------------------------------------------------------------

/// <summary>Body for <see cref="AuthController.SignUp"/>.</summary>
public sealed record SignUpDto(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    string? DisplayName,
    string? TimeZone);

/// <summary>Body for <see cref="AuthController.Verify"/>.</summary>
public sealed record VerifyDto([Required] string Token);

/// <summary>Body for <see cref="AuthController.SignIn"/>.</summary>
public sealed record SignInDto(
    [Required, EmailAddress] string Email,
    [Required] string Password);

/// <summary>Body for <see cref="AuthController.Forgot"/>.</summary>
public sealed record ForgotDto([Required, EmailAddress] string Email);

/// <summary>Body for <see cref="AuthController.Reset"/>.</summary>
public sealed record ResetDto(
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword);

/// <summary>Response shape for sign-in. The refresh token is in the httpOnly cookie, not the body. The role is intentionally absent — the client makes permission decisions via <c>/api/v1/me/permissions</c>.</summary>
public sealed record SignInResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string Status,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt);

/// <summary>Same shape as <see cref="SignInResponse"/>, kept separate to keep the OpenAPI doc stable for the wave-3 frontend codegen.</summary>
public sealed record SignUpResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string Status,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt);
