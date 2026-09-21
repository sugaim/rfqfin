using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

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

