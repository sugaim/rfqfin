using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class PostgreSqlSeedMarkerConfiguration : IEntityTypeConfiguration<SeedMarker>
{
    public void Configure(EntityTypeBuilder<SeedMarker> builder)
    {
        builder.ToTable("seed_markers");
        builder.HasKey(marker => marker.Key);
        builder.Property(marker => marker.Key).HasColumnName("key").HasMaxLength(100);
        builder.Property(marker => marker.AppliedAt).HasColumnName("applied_at")
            .HasColumnType("timestamp with time zone");
    }
}

