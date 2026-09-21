using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal static class PostgreSqlModelConfiguration
{
    public static void Apply(ModelBuilder modelBuilder) =>
        modelBuilder.HasSequence<long>(PostgreSqlCaseIdGenerator.SequenceName);
}

internal sealed class EfCoreRfqCaseConfiguration : IEntityTypeConfiguration<RfqCaseEntity>
{
    public void Configure(EntityTypeBuilder<RfqCaseEntity> builder)
    {
        builder.ToTable("rfq_cases");
        builder.HasKey(entity => entity.CaseId);
        builder.Property(entity => entity.CaseId).HasColumnName("case_id").ValueGeneratedNever();
        builder.Property(entity => entity.ClientId).HasColumnName("client_id").HasMaxLength(100);
        builder.Property(entity => entity.SecurityId).HasColumnName("security_id").HasMaxLength(100);
        builder.Property(entity => entity.CategorySnapshot).HasColumnName("category_snapshot").HasMaxLength(50);
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(entity => entity.SalesId).HasColumnName("sales_id").HasMaxLength(100).IsRequired(false);
        builder.Property(entity => entity.CopiedFromCaseId).HasColumnName("copied_from_case_id");
        builder.HasOne<RfqCaseEntity>().WithMany().HasForeignKey(entity => entity.CopiedFromCaseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.SalesId, entity.CreatedAt })
            .HasDatabaseName("ix_rfq_cases_sales_id_created_at");
    }
}

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
        builder.HasOne<RfqRevisionEntity>().WithMany().HasForeignKey(entity => entity.QuoteSeedRevisionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RfqRevisionEntity>().WithMany().HasForeignKey(entity => entity.CopiedFromRevisionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EfCoreCaseCurrentConfiguration : IEntityTypeConfiguration<CaseCurrentEntity>
{
    public void Configure(EntityTypeBuilder<CaseCurrentEntity> builder)
    {
        builder.ToTable("case_currents");
        builder.HasKey(entity => entity.CaseId);
        builder.Property(entity => entity.CaseId).HasColumnName("case_id").ValueGeneratedNever();
        builder.Property(entity => entity.Lifecycle).HasColumnName("lifecycle").HasConversion<string>().HasMaxLength(30);
        builder.Property(entity => entity.RfqStatus).HasColumnName("rfq_status").HasConversion<string>().HasMaxLength(30);
        builder.Property(entity => entity.QuoteStatus).HasColumnName("quote_status").HasConversion<string>().HasMaxLength(30);
        builder.Property(entity => entity.QuoteRequestReason).HasColumnName("quote_request_reason").HasConversion<string>().HasMaxLength(30);
        builder.Property(entity => entity.CurrentRevisionId).HasColumnName("current_revision_id");
        builder.Property(entity => entity.CurrentQuoteId).HasColumnName("current_quote_id");
        builder.Property(entity => entity.ClosedQuoteId).HasColumnName("closed_quote_id");
        builder.Property(entity => entity.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(entity => entity.ContactOwnerId).HasColumnName("contact_owner_id").HasMaxLength(100);
        builder.Property(entity => entity.AssignedTraderId).HasColumnName("assigned_trader_id").HasMaxLength(100);
        builder.Property(entity => entity.Owned).HasColumnName("owned");
        builder.HasOne(entity => entity.RfqCase).WithOne(entity => entity.Current)
            .HasForeignKey<CaseCurrentEntity>(entity => entity.CaseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.CurrentRevision).WithMany()
            .HasForeignKey(entity => entity.CurrentRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.CurrentQuote).WithMany()
            .HasForeignKey(entity => entity.CurrentQuoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.ClosedQuote).WithMany()
            .HasForeignKey(entity => entity.ClosedQuoteId).OnDelete(DeleteBehavior.Restrict);
    }
}

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
