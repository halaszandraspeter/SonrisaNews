using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonrisaNews.Domain.Entities;

namespace SonrisaNews.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for the <see cref="AlertChannelMode"/> join. The (AlertId,
/// ChannelId) pair is the primary key — that gives us the unique row per
/// (alert, channel) for free and is the index that backs the channel-mode
/// matrix read path.
/// </summary>
internal class AlertChannelModeConfiguration : IEntityTypeConfiguration<AlertChannelMode>
{
    public void Configure(EntityTypeBuilder<AlertChannelMode> builder)
    {
        builder.ToTable("AlertChannelModes");
        builder.HasKey(acm => new { acm.AlertId, acm.ChannelId });

        builder.Property(acm => acm.Mode)
            .HasConversion<int>();

        builder.HasOne<Alert>()
            .WithMany()
            .HasForeignKey(acm => acm.AlertId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Channel>()
            .WithMany()
            .HasForeignKey(acm => acm.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
