using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonrisaNews.Domain.Entities;

namespace SonrisaNews.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="Event"/>. The required composite index <c>(SourceId, OccurredAt)</c>
/// backs the matcher window query (<c>1-features.md</c> §4). A unique index on
/// <c>(SourceId, ExternalId)</c> backs the dedupe rule from <c>1-features.md</c> §5.</summary>
internal class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ExternalId)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(e => e.Type)
            .HasConversion<int>();

        builder.Property(e => e.Payload)
            .IsRequired()
            .HasColumnType("TEXT");

        builder.HasOne<Source>()
            .WithMany()
            .HasForeignKey(e => e.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.SourceId, e.OccurredAt })
            .HasDatabaseName("IX_Events_SourceId_OccurredAt");

        builder.HasIndex(e => new { e.SourceId, e.ExternalId })
            .IsUnique()
            .HasDatabaseName("UX_Events_SourceId_ExternalId");
    }
}
