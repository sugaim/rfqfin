using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal sealed class EfCoreClientConfiguration : IEntityTypeConfiguration<ClientEntity>
{
    public void Configure(EntityTypeBuilder<ClientEntity> builder)
    {
        builder.ToTable("clients");
        builder.HasKey(entity => entity.ClientId);
        builder.Property(entity => entity.ClientId).HasColumnName("client_id").HasMaxLength(100);
        builder.Property(entity => entity.Code).HasColumnName("code").HasMaxLength(50);
        builder.Property(entity => entity.Name).HasColumnName("name").HasMaxLength(200);
        builder.HasIndex(entity => entity.Code).IsUnique().HasDatabaseName("ux_clients_code");
    }
}

