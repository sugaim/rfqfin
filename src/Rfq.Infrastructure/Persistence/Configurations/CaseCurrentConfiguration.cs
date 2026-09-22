using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

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
        builder.Property(entity => entity.ClosedBusinessDate)
            .HasColumnName("closed_business_date").HasColumnType("date");
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
        builder.HasIndex(entity => entity.RfqStatus)
            .HasDatabaseName("ix_case_currents_rfq_status");
    }
}

