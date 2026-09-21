using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class EfCoreCaseMemoConfiguration : IEntityTypeConfiguration<CaseMemoEntity>
{
    public void Configure(EntityTypeBuilder<CaseMemoEntity> builder)
    {
        builder.ToTable("case_memos");
        builder.HasKey(entity => entity.CaseId);
        builder.Property(entity => entity.CaseId).HasColumnName("case_id").ValueGeneratedNever();
        builder.Property(entity => entity.SalesMemo).HasColumnName("sales_memo");
        builder.Property(entity => entity.TraderMemo).HasColumnName("trader_memo");
        builder.Property(entity => entity.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasOne(entity => entity.RfqCase).WithOne(entity => entity.Memo)
            .HasForeignKey<CaseMemoEntity>(entity => entity.CaseId).OnDelete(DeleteBehavior.Cascade);
    }
}

