namespace SonrisaNews.Domain.Entities;

/// <summary>
/// A user-defined rule that fires when upstream events match its filters.
/// Each Alert belongs to one User and is of one <see cref="AlertType"/>;
/// the per-type filter shape is stored as a JSON blob in <see cref="Filters"/>
/// and validated per type by FluentValidation in wave 5.
/// </summary>
public class Alert
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>User-chosen label shown in the dashboard.</summary>
    public string Name { get; set; } = string.Empty;

    public AlertType Type { get; set; }

    /// <summary>Master switch. False = the matcher skips this alert entirely.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Type-specific filter document as a JSON string. The shape is validated
    /// per <see cref="Type"/> in wave 5. Stored as <c>TEXT</c> on SQLite and
    /// <c>jsonb</c> on Postgres (see <see cref="Infrastructure.Persistence.Configurations.AlertConfiguration"/>).
    /// </summary>
    public string Filters { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
