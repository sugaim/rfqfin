using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class PostgreSqlWorkingQuoteConfiguration : IEntityTypeConfiguration<WorkingQuoteEntity>
{
    public void Configure(EntityTypeBuilder<WorkingQuoteEntity> builder)
    {
        builder.ToTable("working_quotes");
        builder.HasKey(entity => entity.RevisionId);
        builder.Property(entity => entity.RevisionId).HasColumnName("revision_id").ValueGeneratedNever();
        builder.Property(entity => entity.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(entity => entity.Mode).HasColumnName("mode").HasConversion<string>().HasMaxLength(30);
        builder.Property(entity => entity.CalculatedPayloadJson).HasColumnName("calculated_payload").HasColumnType("jsonb");
        builder.Property(entity => entity.ManualPayloadJson).HasColumnName("manual_payload").HasColumnType("jsonb");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        builder.HasOne(entity => entity.Revision).WithOne()
            .HasForeignKey<WorkingQuoteEntity>(entity => entity.RevisionId).OnDelete(DeleteBehavior.Cascade);
    }
}

