using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class PostgreSqlEventConfiguration : IEntityTypeConfiguration<EventEntity>
{
    public void Configure(EntityTypeBuilder<EventEntity> builder)
    {
        builder.ToTable("events");
        builder.HasKey(entity => entity.EventId);
        builder.Property(entity => entity.EventId).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(entity => entity.OccurredAt).HasColumnName("occurred_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.ActorUserId).HasColumnName("actor_user_id").HasMaxLength(100);
    }
}

