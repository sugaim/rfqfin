using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class PostgreSqlMasterUserConfiguration : IEntityTypeConfiguration<MasterUserEntity>
{
    public void Configure(EntityTypeBuilder<MasterUserEntity> builder)
    {
        builder.ToTable("master_users");
        builder.HasKey(entity => entity.UserId);
        builder.Property(entity => entity.UserId).HasColumnName("user_id").HasMaxLength(100);
        builder.Property(entity => entity.Name).HasColumnName("name").HasMaxLength(100);
        builder.Property(entity => entity.DeskId).HasColumnName("desk_id").HasMaxLength(50);
        builder.Property(entity => entity.Roles).HasColumnName("roles").HasColumnType("text[]");
        builder.Property(entity => entity.DefaultQuoteExpiryMinutes).HasColumnName("default_quote_expiry_minutes");
        builder.Property(entity => entity.DefaultQuoteMode).HasColumnName("default_quote_mode")
            .HasConversion<string>().HasMaxLength(20);
        builder.Property(entity => entity.Theme).HasColumnName("theme")
            .HasConversion<string>().HasMaxLength(20);
        builder.HasOne<DeskEntity>().WithMany().HasForeignKey(entity => entity.DeskId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
