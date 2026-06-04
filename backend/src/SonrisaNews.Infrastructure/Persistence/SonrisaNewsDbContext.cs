using Microsoft.EntityFrameworkCore;
using SonrisaNews.Domain.Entities;

namespace SonrisaNews.Infrastructure.Persistence;

/// <summary>
/// Sonrisa News EF Core database context. Owns the <see cref="DbSet{TEntity}"/>
/// surface; entity mappings live in <c>Configurations/*</c> and are discovered
/// by <see cref="OnModelCreating"/> via <c>ApplyConfigurationsFromAssembly</c>.
/// </summary>
public class SonrisaNewsDbContext(DbContextOptions<SonrisaNewsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertChannelMode> AlertChannelModes => Set<AlertChannelMode>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<EmailVerification> EmailVerifications => Set<EmailVerification>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Wave 2: all entity mappings come from IEntityTypeConfiguration
        // implementations in the Persistence/Configurations folder. Adding a
        // new entity means (1) the class in Domain/Entities, (2) a config in
        // Persistence/Configurations, (3) a DbSet<T> above. ApplyConfigurationsFromAssembly
        // does the rest.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SonrisaNewsDbContext).Assembly);
    }
}
