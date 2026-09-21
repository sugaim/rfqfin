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

internal sealed class PostgreSqlMasterUserConfiguration : IEntityTypeConfiguration<MasterUserEntity>
{
    public void Configure(EntityTypeBuilder<MasterUserEntity> builder)
    {
        builder.ToTable("master_users");
        builder.HasKey(entity => entity.UserId);
        builder.Property(entity => entity.UserId).HasColumnName("user_id").HasMaxLength(100);
        builder.Property(entity => entity.Name).HasColumnName("name").HasMaxLength(100);
        builder.Property(entity => entity.DeskId).HasColumnName("desk_id").HasMaxLength(50);
        builder.Property(entity => entity.Roles).HasColumnName("roles").HasColumnType("text[]");
        builder.Property(entity => entity.DefaultQuoteExpiryMinutes).HasColumnName("default_quote_expiry_minutes");
        builder.HasOne<DeskEntity>().WithMany().HasForeignKey(entity => entity.DeskId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EfCoreCategoryConfiguration : IEntityTypeConfiguration<CategoryEntity>
{
    public void Configure(EntityTypeBuilder<CategoryEntity> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(entity => entity.CategoryId);
        builder.Property(entity => entity.CategoryId).HasColumnName("category_id").HasMaxLength(50);
        builder.Property(entity => entity.Name).HasColumnName("name").HasMaxLength(100);
    }
}

internal sealed class EfCoreCategoryRoutingConfiguration
    : IEntityTypeConfiguration<CategoryRoutingEntity>
{
    public void Configure(EntityTypeBuilder<CategoryRoutingEntity> builder)
    {
        builder.ToTable("category_routings");
        builder.HasKey(entity => entity.CategoryId);
        builder.Property(entity => entity.CategoryId).HasColumnName("category_id").HasMaxLength(50);
        builder.Property(entity => entity.DefaultTraderId).HasColumnName("default_trader_id").HasMaxLength(100);
        builder.HasOne<CategoryEntity>().WithOne().HasForeignKey<CategoryRoutingEntity>(entity => entity.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MasterUserEntity>().WithMany().HasForeignKey(entity => entity.DefaultTraderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EfCoreClientConfiguration : IEntityTypeConfiguration<ClientEntity>
{
    public void Configure(EntityTypeBuilder<ClientEntity> builder)
    {
        builder.ToTable("clients");
        builder.HasKey(entity => entity.ClientId);
        builder.Property(entity => entity.ClientId).HasColumnName("client_id").HasMaxLength(100);
        builder.Property(entity => entity.Code).HasColumnName("code").HasMaxLength(50);
        builder.Property(entity => entity.Name).HasColumnName("name").HasMaxLength(200);
        builder.HasIndex(entity => entity.Code).IsUnique().HasDatabaseName("ux_clients_code");
    }
}

internal sealed class EfCoreSecurityConfiguration : IEntityTypeConfiguration<SecurityEntity>
{
    public void Configure(EntityTypeBuilder<SecurityEntity> builder)
    {
        builder.ToTable("securities");
        builder.HasKey(entity => entity.SecurityId);
        builder.Property(entity => entity.SecurityId).HasColumnName("security_id").HasMaxLength(100);
        builder.Property(entity => entity.JapaneseName).HasColumnName("japanese_name").HasMaxLength(200);
        builder.Property(entity => entity.BbgDisplay).HasColumnName("bbg_display").HasMaxLength(200);
        builder.Property(entity => entity.BbgSearchText).HasColumnName("bbg_search_text").HasMaxLength(200);
        builder.Property(entity => entity.InternalCode).HasColumnName("internal_code").HasMaxLength(30);
        builder.Property(entity => entity.Isin).HasColumnName("isin").HasMaxLength(12);
        builder.Property(entity => entity.CategoryId).HasColumnName("category_id").HasMaxLength(50);
        builder.HasIndex(entity => entity.InternalCode).IsUnique()
            .HasDatabaseName("ux_securities_internal_code");
        builder.HasIndex(entity => entity.Isin).IsUnique().HasDatabaseName("ux_securities_isin");
        builder.HasOne(entity => entity.Category).WithMany().HasForeignKey(entity => entity.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EfCoreSystemDateConfiguration : IEntityTypeConfiguration<SystemDateEntity>
{
    public void Configure(EntityTypeBuilder<SystemDateEntity> builder)
    {
        builder.ToTable("system_dates");
        builder.HasKey(entity => entity.Key);
        builder.Property(entity => entity.Key).HasColumnName("key").HasMaxLength(50);
        builder.Property(entity => entity.BusinessDate).HasColumnName("business_date").HasColumnType("date");
    }
}
