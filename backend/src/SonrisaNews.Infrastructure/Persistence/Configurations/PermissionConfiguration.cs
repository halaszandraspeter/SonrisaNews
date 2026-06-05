using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonrisaNews.Domain.Auth;

namespace SonrisaNews.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="Permission"/>. Seeded at migration time: one row per constant in <see cref="Permissions"/>.</summary>
internal class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(120);   // longest permission name in Permissions.cs is well under this.

        builder.Property(p => p.Description)
            .HasMaxLength(500);

        builder.HasIndex(p => p.Name)
            .IsUnique()
            .HasDatabaseName("UX_Permissions_Name");
    }
}
