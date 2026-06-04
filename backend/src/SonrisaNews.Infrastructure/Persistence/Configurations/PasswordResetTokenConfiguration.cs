using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonrisaNews.Domain.Entities;

namespace SonrisaNews.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="PasswordResetToken"/>.</summary>
internal class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("PasswordResetTokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Token)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.UserId)
            .HasDatabaseName("IX_PasswordResetTokens_UserId");

        builder.HasIndex(t => t.Token)
            .IsUnique()
            .HasDatabaseName("UX_PasswordResetTokens_Token");
    }
}
