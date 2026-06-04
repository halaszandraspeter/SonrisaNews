using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonrisaNews.Domain.Entities;

namespace SonrisaNews.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="Channel"/>.</summary>
internal class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.ToTable("Channels");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Destination)
            .IsRequired()
            .HasMaxLength(2048);   // Slack incoming-webhook URLs comfortably fit; emails are < 320.

        builder.Property(c => c.Type)
            .HasConversion<int>();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.UserId)
            .HasDatabaseName("IX_Channels_UserId");
    }
}
