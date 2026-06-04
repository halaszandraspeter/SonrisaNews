using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonrisaNews.Domain.Entities;

namespace SonrisaNews.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="AuditLog"/>. No FK to <c>Users</c> on purpose: the audit log must survive
/// user deletion (an admin action that targets a deleted user is still auditable).</summary>
internal class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(a => a.TargetType)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(a => a.TargetId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(a => a.Metadata)
            .IsRequired()
            .HasColumnType("TEXT");

        builder.HasIndex(a => new { a.ActorUserId, a.CreatedAt })
            .HasDatabaseName("IX_AuditLogs_ActorUserId_CreatedAt");

        builder.HasIndex(a => a.CreatedAt)
            .HasDatabaseName("IX_AuditLogs_CreatedAt");
    }
}
