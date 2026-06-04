using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonrisaNews.Domain.Entities;

namespace SonrisaNews.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="Source"/>. <see cref="Source.Config"/> is JSON, stored as <c>TEXT</c> on
/// SQLite and a swap-point to <c>jsonb</c> on Postgres (<c>2-stack.md</c> §5).</summary>
internal class SourceConfiguration : IEntityTypeConfiguration<Source>
{
    public void Configure(EntityTypeBuilder<Source> builder)
    {
        builder.ToTable("Sources");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Type)
            .HasConversion<int>();

        builder.Property(s => s.Config)
            .IsRequired()
            .HasColumnType("TEXT");

        builder.HasIndex(s => new { s.Type, s.Enabled })
            .HasDatabaseName("IX_Sources_Type_Enabled");
    }
}
