using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

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
        builder.Property(entity => entity.CreatedBusinessDate)
            .HasColumnName("created_business_date").HasColumnType("date");
        builder.Property(entity => entity.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(entity => entity.SalesId).HasColumnName("sales_id").HasMaxLength(100).IsRequired(false);
        builder.Property(entity => entity.CopiedFromCaseId).HasColumnName("copied_from_case_id");
        builder.HasOne<RfqCaseEntity>().WithMany().HasForeignKey(entity => entity.CopiedFromCaseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.SalesId, entity.CreatedAt })
            .HasDatabaseName("ix_rfq_cases_sales_id_created_at");
        builder.HasIndex(entity => entity.CreatedBusinessDate)
            .HasDatabaseName("ix_rfq_cases_created_business_date");
    }
}

