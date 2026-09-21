using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class PostgreSqlEventCursorConfiguration : IEntityTypeConfiguration<EventCursorEntity>
{
    public void Configure(EntityTypeBuilder<EventCursorEntity> builder)
    {
        builder.ToTable("event_cursors");
        builder.HasKey(entity => entity.CursorKey);
        builder.Property(entity => entity.CursorKey).HasColumnName("cursor_key").HasMaxLength(30);
        builder.Property(entity => entity.LastEventId).HasColumnName("last_event_id");
        builder.HasData(new EventCursorEntity { CursorKey = "global", LastEventId = 0 });
    }
}

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

internal sealed class PostgreSqlUserGridConfigConfiguration
    : IEntityTypeConfiguration<UserGridConfigEntity>
{
    public void Configure(EntityTypeBuilder<UserGridConfigEntity> builder)
    {
        builder.ToTable("user_grid_configs");
        builder.HasKey(entity => new { entity.UserId, entity.ScreenId, entity.ConfigKey });
        builder.Property(entity => entity.UserId).HasColumnName("user_id").HasMaxLength(100);
        builder.Property(entity => entity.ScreenId).HasColumnName("screen_id").HasMaxLength(100);
        builder.Property(entity => entity.ConfigKey).HasColumnName("config_key").HasMaxLength(100);
        builder.Property(entity => entity.Version).HasColumnName("version");
        builder.Property(entity => entity.ConfigJson).HasColumnName("config_json").HasColumnType("jsonb");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");
    }
}
