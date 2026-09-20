using Microsoft.EntityFrameworkCore;

namespace Rfq.Infrastructure;

public sealed class RfqDbContext(DbContextOptions<RfqDbContext> options) : DbContext(options)
{
    public DbSet<SeedMarker> SeedMarkers => Set<SeedMarker>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var seedMarker = modelBuilder.Entity<SeedMarker>();
        seedMarker.ToTable("seed_markers");
        seedMarker.HasKey(marker => marker.Key);
        seedMarker.Property(marker => marker.Key)
            .HasColumnName("key")
            .HasMaxLength(100);
        seedMarker.Property(marker => marker.AppliedAt)
            .HasColumnName("applied_at")
            .HasColumnType("timestamp with time zone");
    }
}
