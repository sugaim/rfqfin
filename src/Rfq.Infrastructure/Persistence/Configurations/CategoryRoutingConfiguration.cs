using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

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

