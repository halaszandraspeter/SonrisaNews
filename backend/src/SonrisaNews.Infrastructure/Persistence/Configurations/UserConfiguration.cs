using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Entities;

namespace SonrisaNews.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="User"/>. The unique index on <see cref="User.Email"/>
/// is the only non-FK index the entity owns; FKs to <c>User</c> are declared on
/// the dependent side (alerts, channels, etc.).
/// </summary>
internal class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(320);   // RFC 5321 SMTP path limit; covers the longest legal address.

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);   // Argon2id PHC string, with room for future params.

        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(u => u.TimeZone)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(u => u.Status)
            .HasConversion<int>()
            .HasDefaultValue(UserStatus.PendingEmailVerification);

        builder.Property(u => u.MustChangePassword)
            .HasConversion<int>()
            .HasDefaultValue(false);

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("UX_Users_Email");
    }
}
