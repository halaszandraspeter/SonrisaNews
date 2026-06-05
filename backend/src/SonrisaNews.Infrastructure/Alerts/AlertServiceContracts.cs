using SonrisaNews.Domain;
using SonrisaNews.Domain.Alerts;

namespace SonrisaNews.Infrastructure.Alerts;

/// <summary>Outcome of an alert-service call. The controller maps these to HTTP status codes.</summary>
public enum AlertOutcome
{
    /// <summary>Operation succeeded.</summary>
    Success,

    /// <summary>The caller is not signed in. Maps to 401.</summary>
    Unauthenticated,

    /// <summary>The filter JSON is invalid. Maps to 400.</summary>
    InvalidFilter,

    /// <summary>The alert does not exist OR does not belong to the caller. Maps to 404 (no existence leak).</summary>
    NotFound,

    /// <summary>The referenced channel does not exist OR does not belong to the caller. Maps to 404.</summary>
    ChannelNotFound,
}

/// <summary>Result envelope for alert-service operations that may fail with a typed outcome.</summary>
/// <typeparam name="TPayload">The success payload (e.g. a single <c>Alert</c>, a list, or void for deletes).</typeparam>
public sealed record AlertResult<TPayload>(
    AlertOutcome Outcome,
    TPayload? Payload = default,
    IReadOnlyList<AlertFiltersError>? Errors = null)
{
    public bool IsSuccess => Outcome == AlertOutcome.Success;

    public static AlertResult<TPayload> Success(TPayload payload) =>
        new(AlertOutcome.Success, Payload: payload);

    public static AlertResult<TPayload> Failure(AlertOutcome outcome) =>
        new(outcome);

    public static AlertResult<TPayload> Failure(AlertOutcome outcome, IReadOnlyList<AlertFiltersError> errors) =>
        new(outcome, Errors: errors);
}

/// <summary>Input shape for <see cref="IAlertService.CreateAsync"/>. Owned by the controller; mapped from the request DTO.</summary>
public sealed record CreateAlertInput(
    string Name,
    AlertType Type,
    string Filters);

/// <summary>Input shape for <see cref="IAlertService.UpdateAsync"/>. Same fields as create; <c>Name</c> + <c>Filters</c> + <c>Enabled</c> are mutable.</summary>
public sealed record UpdateAlertInput(
    string? Name,
    string? Filters,
    bool? Enabled);

/// <summary>Input shape for <see cref="IAlertService.SetChannelModeAsync"/>.</summary>
public sealed record SetChannelModeInput(
    Guid ChannelId,
    DeliveryMode Mode);
