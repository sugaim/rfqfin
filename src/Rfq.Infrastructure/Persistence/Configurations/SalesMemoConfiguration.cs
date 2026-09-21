using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class EfCoreSalesMemoConfiguration : IEntityTypeConfiguration<SalesMemoEntity>
{
    public void Configure(EntityTypeBuilder<SalesMemoEntity> builder)
    {
        builder.ToTable("sales_memos");
        builder.HasKey(entity => entity.CaseId);
        builder.Property(entity => entity.CaseId).HasColumnName("case_id").ValueGeneratedNever();
        builder.Property(entity => entity.Value).HasColumnName("value");
        builder.Property(entity => entity.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasOne(entity => entity.RfqCase).WithOne(entity => entity.SalesMemo)
            .HasForeignKey<SalesMemoEntity>(entity => entity.CaseId).OnDelete(DeleteBehavior.Cascade);
    }
}
