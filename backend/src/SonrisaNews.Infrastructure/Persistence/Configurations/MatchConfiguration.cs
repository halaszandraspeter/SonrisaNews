using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonrisaNews.Domain.Entities;

namespace SonrisaNews.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="Match"/>. The required composite index <c>(AlertId, FiredAt)</c>
/// backs the "what fired recently" view (<c>1-features.md</c> §4). The unique
/// index on <c>(AlertId, EventId)</c> is the idempotency guard for the
/// dispatcher (1-features.md §6 reliability rule).</summary>
internal class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("Matches");
        builder.HasKey(m => m.Id);

        builder.HasOne<Alert>()
            .WithMany()
            .HasForeignKey(m => m.AlertId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(m => m.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.AlertId, m.FiredAt })
            .HasDatabaseName("IX_Matches_AlertId_FiredAt");

        builder.HasIndex(m => new { m.AlertId, m.EventId })
            .IsUnique()
            .HasDatabaseName("UX_Matches_AlertId_EventId");
    }
}
