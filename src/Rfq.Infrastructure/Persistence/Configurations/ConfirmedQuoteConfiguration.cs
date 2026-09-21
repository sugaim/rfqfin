using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class PostgreSqlConfirmedQuoteConfiguration : IEntityTypeConfiguration<ConfirmedQuoteEntity>
{
    public void Configure(EntityTypeBuilder<ConfirmedQuoteEntity> builder)
    {
        builder.ToTable("confirmed_quotes");
        builder.HasKey(entity => entity.QuoteId);
        builder.Property(entity => entity.QuoteId).HasColumnName("quote_id").ValueGeneratedNever();
        builder.Property(entity => entity.RevisionId).HasColumnName("revision_id");
        builder.Property(entity => entity.SecurityId).HasColumnName("security_id").HasMaxLength(100);
        builder.Property(entity => entity.SettlementDate).HasColumnName("settlement_date").HasColumnType("date");
        builder.Property(entity => entity.ConfirmedBy).HasColumnName("confirmed_by").HasMaxLength(100);
        builder.Property(entity => entity.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.Mode).HasColumnName("mode").HasConversion<string>().HasMaxLength(30);
        builder.Property(entity => entity.CalculatedPayloadJson).HasColumnName("calculated_payload").HasColumnType("jsonb");
        builder.Property(entity => entity.ManualPayloadJson).HasColumnName("manual_payload").HasColumnType("jsonb");
        builder.Property(entity => entity.ExpiryMinutes).HasColumnName("expiry_minutes");
        builder.Property(entity => entity.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.RequestReasonAnswered).HasColumnName("request_reason_answered")
            .HasConversion<string>().HasMaxLength(30);
        builder.HasOne(entity => entity.Revision).WithMany()
            .HasForeignKey(entity => entity.RevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.RevisionId, entity.ConfirmedAt })
            .HasDatabaseName("ix_confirmed_quotes_revision_id_confirmed_at");
    }
}

