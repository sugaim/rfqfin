using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class PostgreSqlQuoteEventConfiguration : IEntityTypeConfiguration<QuoteEventEntity>
{
    public void Configure(EntityTypeBuilder<QuoteEventEntity> builder)
    {
        builder.ToTable("quote_events");
        builder.HasKey(entity => entity.EventId);
        builder.Property(entity => entity.EventId).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(entity => entity.QuoteId).HasColumnName("quote_id");
        builder.Property(entity => entity.Type).HasColumnName("type").HasMaxLength(100);
        builder.Property(entity => entity.PayloadJson).HasColumnName("payload").HasColumnType("jsonb");
        builder.HasOne(entity => entity.Event).WithOne()
            .HasForeignKey<QuoteEventEntity>(entity => entity.EventId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ConfirmedQuoteEntity>().WithMany().HasForeignKey(entity => entity.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

