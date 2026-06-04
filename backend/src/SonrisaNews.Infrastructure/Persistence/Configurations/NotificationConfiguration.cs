using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Entities;

namespace SonrisaNews.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="Notification"/>. The composite index
/// <c>(UserId, SentAt)</c> backs the activity feed
/// (<c>1-features.md</c> §4): the btree satisfies the reverse-direction
/// <c>ORDER BY SentAt DESC</c> scan on both SQLite and Postgres, so the
/// index serves the typical "newest first" feed read.
/// </summary>
internal class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Status)
            .HasConversion<int>();

        builder.Property(n => n.Mode)
            .HasConversion<int>();

        builder.HasOne<Match>()
            .WithMany()
            .HasForeignKey(n => n.MatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => new { n.UserId, n.SentAt })
            .HasDatabaseName("IX_Notifications_UserId_SentAt");

        builder.HasIndex(n => n.MatchId)
            .HasDatabaseName("IX_Notifications_MatchId");
    }
}
