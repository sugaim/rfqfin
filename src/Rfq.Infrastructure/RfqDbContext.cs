using Microsoft.EntityFrameworkCore;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class RfqDbContext(DbContextOptions<RfqDbContext> options) : DbContext(options)
{
    public DbSet<SeedMarker> SeedMarkers => Set<SeedMarker>();

    internal DbSet<RfqCaseEntity> RfqCases => Set<RfqCaseEntity>();

    internal DbSet<CaseCurrentEntity> CaseCurrents => Set<CaseCurrentEntity>();

    internal DbSet<RfqRevisionEntity> RfqRevisions => Set<RfqRevisionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>(PostgreSqlCaseIdGenerator.SequenceName);

        var seedMarker = modelBuilder.Entity<SeedMarker>();
        seedMarker.ToTable("seed_markers");
        seedMarker.HasKey(marker => marker.Key);
        seedMarker.Property(marker => marker.Key)
            .HasColumnName("key")
            .HasMaxLength(100);
        seedMarker.Property(marker => marker.AppliedAt)
            .HasColumnName("applied_at")
            .HasColumnType("timestamp with time zone");

        var rfqCase = modelBuilder.Entity<RfqCaseEntity>();
        rfqCase.ToTable("rfq_cases");
        rfqCase.HasKey(entity => entity.CaseId);
        rfqCase.Property(entity => entity.CaseId)
            .HasColumnName("case_id")
            .ValueGeneratedNever();
        rfqCase.Property(entity => entity.ClientId)
            .HasColumnName("client_id")
            .HasMaxLength(100);
        rfqCase.Property(entity => entity.SecurityId)
            .HasColumnName("security_id")
            .HasMaxLength(100);
        rfqCase.Property(entity => entity.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone");
        rfqCase.Property(entity => entity.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100);
        rfqCase.Property(entity => entity.SalesId)
            .HasColumnName("sales_id")
            .HasMaxLength(100);
        rfqCase.HasIndex(entity => new { entity.SalesId, entity.CreatedAt })
            .HasDatabaseName("ix_rfq_cases_sales_id_created_at");

        var revision = modelBuilder.Entity<RfqRevisionEntity>();
        revision.ToTable("rfq_revisions");
        revision.HasKey(entity => entity.RevisionId);
        revision.Property(entity => entity.RevisionId)
            .HasColumnName("revision_id")
            .ValueGeneratedNever();
        revision.Property(entity => entity.CaseId)
            .HasColumnName("case_id");
        revision.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30);
        revision.Property(entity => entity.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();
        revision.Property(entity => entity.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone");
        revision.Property(entity => entity.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100);
        revision.HasOne(entity => entity.RfqCase)
            .WithMany(entity => entity.Revisions)
            .HasForeignKey(entity => entity.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
        revision.HasIndex(entity => entity.CaseId)
            .IsUnique()
            .HasFilter("status = 'Draft'")
            .HasDatabaseName("ux_rfq_revisions_one_draft_per_case");

        var current = modelBuilder.Entity<CaseCurrentEntity>();
        current.ToTable("case_currents");
        current.HasKey(entity => entity.CaseId);
        current.Property(entity => entity.CaseId)
            .HasColumnName("case_id")
            .ValueGeneratedNever();
        current.Property(entity => entity.Lifecycle)
            .HasColumnName("lifecycle")
            .HasConversion<string>()
            .HasMaxLength(30);
        current.Property(entity => entity.RfqStatus)
            .HasColumnName("rfq_status")
            .HasConversion<string>()
            .HasMaxLength(30);
        current.Property(entity => entity.CurrentRevisionId)
            .HasColumnName("current_revision_id");
        current.Property(entity => entity.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();
        current.HasOne(entity => entity.RfqCase)
            .WithOne(entity => entity.Current)
            .HasForeignKey<CaseCurrentEntity>(entity => entity.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
        current.HasOne(entity => entity.CurrentRevision)
            .WithMany()
            .HasForeignKey(entity => entity.CurrentRevisionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RfqCaseEntity
{
    public long CaseId { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string SecurityId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public string SalesId { get; set; } = string.Empty;

    public List<RfqRevisionEntity> Revisions { get; set; } = [];

    public CaseCurrentEntity Current { get; set; } = null!;
}

internal sealed class CaseCurrentEntity
{
    public long CaseId { get; set; }

    public RfqLifecycleKind Lifecycle { get; set; }

    public RfqStatus RfqStatus { get; set; }

    public Guid CurrentRevisionId { get; set; }

    public long Version { get; set; }

    public RfqCaseEntity RfqCase { get; set; } = null!;

    public RfqRevisionEntity CurrentRevision { get; set; } = null!;
}

internal sealed class RfqRevisionEntity
{
    public Guid RevisionId { get; set; }

    public long CaseId { get; set; }

    public RevisionStatus Status { get; set; }

    public long Version { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public RfqCaseEntity RfqCase { get; set; } = null!;
}
