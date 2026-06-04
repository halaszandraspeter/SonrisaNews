using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SonrisaNews.Domain.Entities;

namespace SonrisaNews.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="EmailVerification"/>.</summary>
internal class EmailVerificationConfiguration : IEntityTypeConfiguration<EmailVerification>
{
    public void Configure(EntityTypeBuilder<EmailVerification> builder)
    {
        builder.ToTable("EmailVerifications");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Token)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_EmailVerifications_UserId");

        builder.HasIndex(e => e.Token)
            .IsUnique()
            .HasDatabaseName("UX_EmailVerifications_Token");
    }
}
