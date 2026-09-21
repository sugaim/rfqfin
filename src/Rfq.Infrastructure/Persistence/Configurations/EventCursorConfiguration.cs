using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class EfCoreEventCursorConfiguration : IEntityTypeConfiguration<EventCursorEntity>
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

