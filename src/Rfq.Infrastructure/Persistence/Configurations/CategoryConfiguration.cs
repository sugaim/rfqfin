using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

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

