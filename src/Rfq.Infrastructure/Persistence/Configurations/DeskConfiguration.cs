using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class EfCoreDeskConfiguration : IEntityTypeConfiguration<DeskEntity>
{
    public void Configure(EntityTypeBuilder<DeskEntity> builder)
    {
        builder.ToTable("desks");
        builder.HasKey(entity => entity.DeskId);
        builder.Property(entity => entity.DeskId).HasColumnName("desk_id").HasMaxLength(50);
        builder.Property(entity => entity.Name).HasColumnName("name").HasMaxLength(100);
        builder.Property(entity => entity.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(100);
    }
}

