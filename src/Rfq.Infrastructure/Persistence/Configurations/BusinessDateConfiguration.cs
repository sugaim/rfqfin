using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class EfCoreBusinessDateConfiguration : IEntityTypeConfiguration<BusinessDateEntity>
{
    public void Configure(EntityTypeBuilder<BusinessDateEntity> builder)
    {
        builder.ToTable("business_dates");
        builder.HasKey(entity => entity.Key);
        builder.Property(entity => entity.Key).HasColumnName("key").HasMaxLength(50);
        builder.Property(entity => entity.BusinessDate).HasColumnName("business_date").HasColumnType("date");
    }
}
