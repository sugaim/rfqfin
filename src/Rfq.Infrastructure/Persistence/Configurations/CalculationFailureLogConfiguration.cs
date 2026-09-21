using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class PostgreSqlCalculationFailureLogConfiguration
    : IEntityTypeConfiguration<CalculationFailureLogEntity>
{
    public void Configure(EntityTypeBuilder<CalculationFailureLogEntity> builder)
    {
        builder.ToTable("calculation_failure_logs");
        builder.HasKey(entity => entity.FailureLogId);
        builder.Property(entity => entity.FailureLogId).HasColumnName("failure_log_id").ValueGeneratedNever();
        builder.Property(entity => entity.CaseId).HasColumnName("case_id");
        builder.Property(entity => entity.RevisionId).HasColumnName("revision_id");
        builder.Property(entity => entity.TraderId).HasColumnName("trader_id").HasMaxLength(100);
        builder.Property(entity => entity.RequestId).HasColumnName("request_id");
        builder.Property(entity => entity.Driver).HasColumnName("driver").HasConversion<string>().HasMaxLength(50);
        builder.Property(entity => entity.AttemptedValue).HasColumnName("attempted_value").HasPrecision(20, 8);
        builder.Property(entity => entity.SimpleYieldSlide).HasColumnName("simple_yield_slide").HasPrecision(20, 8);
        builder.Property(entity => entity.PriorWorkingQuoteJson).HasColumnName("prior_working_quote").HasColumnType("jsonb");
        builder.Property(entity => entity.RequestJson).HasColumnName("request_payload").HasColumnType("jsonb");
        builder.Property(entity => entity.ErrorCode).HasColumnName("error_code").HasMaxLength(100);
        builder.Property(entity => entity.ErrorMessage).HasColumnName("error_message");
        builder.Property(entity => entity.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone");
        builder.HasOne<RfqCaseEntity>().WithMany().HasForeignKey(entity => entity.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RfqRevisionEntity>().WithMany().HasForeignKey(entity => entity.RevisionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(entity => new { entity.CaseId, entity.OccurredAt })
            .HasDatabaseName("ix_calculation_failure_logs_case_id_occurred_at");
    }
}

