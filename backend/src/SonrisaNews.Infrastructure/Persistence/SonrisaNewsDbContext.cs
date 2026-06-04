using Microsoft.EntityFrameworkCore;

namespace SonrisaNews.Infrastructure.Persistence;

/// <summary>
/// Sonrisa News EF Core database context.
/// Wave 1: this is a skeleton — the entities are added in wave 2.
/// </summary>
public class SonrisaNewsDbContext(DbContextOptions<SonrisaNewsDbContext> options) : DbContext(options)
{
    /// <summary>Override <see cref="OnModelCreating"/> here in wave 2 when the entities land.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Entity configurations are added in wave 2 (Database + persistence skeleton).
    }
}
