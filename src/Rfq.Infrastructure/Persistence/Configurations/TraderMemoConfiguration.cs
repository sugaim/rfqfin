using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class EfCoreTraderMemoConfiguration : IEntityTypeConfiguration<TraderMemoEntity>
{
    public void Configure(EntityTypeBuilder<TraderMemoEntity> builder)
    {
        builder.ToTable("trader_memos");
        builder.HasKey(entity => entity.CaseId);
        builder.Property(entity => entity.CaseId).HasColumnName("case_id").ValueGeneratedNever();
        builder.Property(entity => entity.Value).HasColumnName("value");
        builder.Property(entity => entity.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasOne(entity => entity.RfqCase).WithOne(entity => entity.TraderMemo)
            .HasForeignKey<TraderMemoEntity>(entity => entity.CaseId).OnDelete(DeleteBehavior.Cascade);
    }
}
