using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class PostgreSqlUserGridConfigConfiguration
    : IEntityTypeConfiguration<UserGridConfigEntity>
{
    public void Configure(EntityTypeBuilder<UserGridConfigEntity> builder)
    {
        builder.ToTable("user_grid_configs");
        builder.HasKey(entity => new { entity.UserId, entity.ScreenId, entity.ConfigKey });
        builder.Property(entity => entity.UserId).HasColumnName("user_id").HasMaxLength(100);
        builder.Property(entity => entity.ScreenId).HasColumnName("screen_id").HasMaxLength(100);
        builder.Property(entity => entity.ConfigKey).HasColumnName("config_key").HasMaxLength(100);
        builder.Property(entity => entity.Version).HasColumnName("version");
        builder.Property(entity => entity.ConfigJson).HasColumnName("config_json").HasColumnType("jsonb");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");
    }
}

