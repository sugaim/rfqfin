using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class PostgreSqlRfqEventConfiguration : IEntityTypeConfiguration<RfqEventEntity>
{
    public void Configure(EntityTypeBuilder<RfqEventEntity> builder)
    {
        builder.ToTable("rfq_events");
        builder.HasKey(entity => entity.EventId);
        builder.Property(entity => entity.EventId).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(entity => entity.CaseId).HasColumnName("case_id");
        builder.Property(entity => entity.Type).HasColumnName("type").HasMaxLength(100);
        builder.Property(entity => entity.PayloadJson).HasColumnName("payload").HasColumnType("jsonb");
        builder.HasOne(entity => entity.Event).WithOne()
            .HasForeignKey<RfqEventEntity>(entity => entity.EventId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RfqCaseEntity>().WithMany().HasForeignKey(entity => entity.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

