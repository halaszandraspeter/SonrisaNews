using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonrisaNews.Domain.Entities;

namespace SonrisaNews.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="Alert"/>. The <see cref="Alert.Filters"/> JSON is stored as <c>TEXT</c> on SQLite
/// and is a swap-point to <c>jsonb</c> on Postgres (see <c>2-stack.md</c> §5).</summary>
internal class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("Alerts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Type)
            .HasConversion<int>();

        builder.Property(a => a.Filters)
            .IsRequired()
            .HasColumnType("TEXT");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("IX_Alerts_UserId");
    }
}
