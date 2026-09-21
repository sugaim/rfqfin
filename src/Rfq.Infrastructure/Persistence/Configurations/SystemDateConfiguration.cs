using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

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

