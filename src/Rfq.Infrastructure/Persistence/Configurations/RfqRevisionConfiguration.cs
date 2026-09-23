using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class PostgreSqlRfqRevisionConfiguration : IEntityTypeConfiguration<RfqRevisionEntity>
{
    public void Configure(EntityTypeBuilder<RfqRevisionEntity> builder)
    {
        builder.ToTable("rfq_revisions");
        builder.HasKey(entity => entity.RevisionId);
        builder.Property(entity => entity.RevisionId).HasColumnName("revision_id").ValueGeneratedNever();
        builder.Property(entity => entity.CaseId).HasColumnName("case_id");
        builder.Property(entity => entity.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(entity => entity.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.DraftCreatedBusinessDate)
            .HasColumnName("draft_created_business_date").HasColumnType("date");
        builder.Property(entity => entity.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(entity => entity.SettlementDate).HasColumnName("settlement_date").HasColumnType("date");
        builder.Property(entity => entity.StandardSettlementDate).HasColumnName("standard_settlement_date").HasColumnType("date");
        builder.Property(entity => entity.Notional).HasColumnName("notional").HasPrecision(20, 2);
        builder.Property(entity => entity.SalesAndTradingMessage).HasColumnName("sales_and_trading_message");
        builder.Property(entity => entity.QuoteSeedRevisionId).HasColumnName("quote_seed_revision_id");
        builder.Property(entity => entity.CopiedFromRevisionId).HasColumnName("copied_from_revision_id");
        builder.Property(entity => entity.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.ConfirmedBy).HasColumnName("confirmed_by").HasMaxLength(100);
        builder.HasOne(entity => entity.RfqCase).WithMany(entity => entity.Revisions)
            .HasForeignKey(entity => entity.CaseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(entity => entity.CaseId).IsUnique().HasFilter("status = 'Draft'")
            .HasDatabaseName("ux_rfq_revisions_one_draft_per_case");
        builder.HasIndex(entity => entity.DraftCreatedBusinessDate)
            .HasDatabaseName("ix_rfq_revisions_draft_created_business_date");
        builder.HasOne<RfqRevisionEntity>().WithMany().HasForeignKey(entity => entity.QuoteSeedRevisionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RfqRevisionEntity>().WithMany().HasForeignKey(entity => entity.CopiedFromRevisionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

