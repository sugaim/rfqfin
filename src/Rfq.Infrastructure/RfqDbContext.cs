using Microsoft.EntityFrameworkCore;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class RfqDbContext(DbContextOptions<RfqDbContext> options) : DbContext(options)
{
    public DbSet<SeedMarker> SeedMarkers => Set<SeedMarker>();

    internal DbSet<RfqCaseEntity> RfqCases => Set<RfqCaseEntity>();

    internal DbSet<CaseCurrentEntity> CaseCurrents => Set<CaseCurrentEntity>();

    internal DbSet<RfqRevisionEntity> RfqRevisions => Set<RfqRevisionEntity>();

    internal DbSet<MasterUserEntity> MasterUsers => Set<MasterUserEntity>();

    internal DbSet<DeskEntity> Desks => Set<DeskEntity>();

    internal DbSet<CategoryEntity> Categories => Set<CategoryEntity>();

    internal DbSet<CategoryRoutingEntity> CategoryRoutings => Set<CategoryRoutingEntity>();

    internal DbSet<ClientEntity> Clients => Set<ClientEntity>();

    internal DbSet<SecurityEntity> Securities => Set<SecurityEntity>();

    internal DbSet<SystemDateEntity> SystemDates => Set<SystemDateEntity>();

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
        rfqCase.Property(entity => entity.CategorySnapshot)
            .HasColumnName("category_snapshot")
            .HasMaxLength(50);
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
        revision.Property(entity => entity.SettlementDate)
            .HasColumnName("settlement_date")
            .HasColumnType("date");
        revision.Property(entity => entity.StandardSettlementDate)
            .HasColumnName("standard_settlement_date")
            .HasColumnType("date");
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
        current.Property(entity => entity.ContactOwnerId)
            .HasColumnName("contact_owner_id")
            .HasMaxLength(100);
        current.Property(entity => entity.AssignedTraderId)
            .HasColumnName("assigned_trader_id")
            .HasMaxLength(100);
        current.Property(entity => entity.Owned)
            .HasColumnName("owned");
        current.HasOne(entity => entity.RfqCase)
            .WithOne(entity => entity.Current)
            .HasForeignKey<CaseCurrentEntity>(entity => entity.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
        current.HasOne(entity => entity.CurrentRevision)
            .WithMany()
            .HasForeignKey(entity => entity.CurrentRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        var desk = modelBuilder.Entity<DeskEntity>();
        desk.ToTable("desks");
        desk.HasKey(entity => entity.DeskId);
        desk.Property(entity => entity.DeskId)
            .HasColumnName("desk_id")
            .HasMaxLength(50);
        desk.Property(entity => entity.Name)
            .HasColumnName("name")
            .HasMaxLength(100);
        desk.Property(entity => entity.TimeZoneId)
            .HasColumnName("time_zone_id")
            .HasMaxLength(100);

        var user = modelBuilder.Entity<MasterUserEntity>();
        user.ToTable("master_users");
        user.HasKey(entity => entity.UserId);
        user.Property(entity => entity.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(100);
        user.Property(entity => entity.Name)
            .HasColumnName("name")
            .HasMaxLength(100);
        user.Property(entity => entity.DeskId)
            .HasColumnName("desk_id")
            .HasMaxLength(50);
        user.Property(entity => entity.Roles)
            .HasColumnName("roles")
            .HasColumnType("text[]");
        user.HasOne<DeskEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.DeskId)
            .OnDelete(DeleteBehavior.Restrict);

        var category = modelBuilder.Entity<CategoryEntity>();
        category.ToTable("categories");
        category.HasKey(entity => entity.CategoryId);
        category.Property(entity => entity.CategoryId)
            .HasColumnName("category_id")
            .HasMaxLength(50);
        category.Property(entity => entity.Name)
            .HasColumnName("name")
            .HasMaxLength(100);

        var routing = modelBuilder.Entity<CategoryRoutingEntity>();
        routing.ToTable("category_routings");
        routing.HasKey(entity => entity.CategoryId);
        routing.Property(entity => entity.CategoryId)
            .HasColumnName("category_id")
            .HasMaxLength(50);
        routing.Property(entity => entity.DefaultTraderId)
            .HasColumnName("default_trader_id")
            .HasMaxLength(100);
        routing.HasOne<CategoryEntity>()
            .WithOne()
            .HasForeignKey<CategoryRoutingEntity>(entity => entity.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
        routing.HasOne<MasterUserEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.DefaultTraderId)
            .OnDelete(DeleteBehavior.Restrict);

        var client = modelBuilder.Entity<ClientEntity>();
        client.ToTable("clients");
        client.HasKey(entity => entity.ClientId);
        client.Property(entity => entity.ClientId)
            .HasColumnName("client_id")
            .HasMaxLength(100);
        client.Property(entity => entity.Code)
            .HasColumnName("code")
            .HasMaxLength(50);
        client.Property(entity => entity.Name)
            .HasColumnName("name")
            .HasMaxLength(200);
        client.HasIndex(entity => entity.Code)
            .IsUnique()
            .HasDatabaseName("ux_clients_code");

        var security = modelBuilder.Entity<SecurityEntity>();
        security.ToTable("securities");
        security.HasKey(entity => entity.SecurityId);
        security.Property(entity => entity.SecurityId)
            .HasColumnName("security_id")
            .HasMaxLength(100);
        security.Property(entity => entity.JapaneseName)
            .HasColumnName("japanese_name")
            .HasMaxLength(200);
        security.Property(entity => entity.BbgDisplay)
            .HasColumnName("bbg_display")
            .HasMaxLength(200);
        security.Property(entity => entity.BbgSearchText)
            .HasColumnName("bbg_search_text")
            .HasMaxLength(200);
        security.Property(entity => entity.InternalCode)
            .HasColumnName("internal_code")
            .HasMaxLength(30);
        security.Property(entity => entity.Isin)
            .HasColumnName("isin")
            .HasMaxLength(12);
        security.Property(entity => entity.CategoryId)
            .HasColumnName("category_id")
            .HasMaxLength(50);
        security.HasIndex(entity => entity.InternalCode)
            .IsUnique()
            .HasDatabaseName("ux_securities_internal_code");
        security.HasIndex(entity => entity.Isin)
            .IsUnique()
            .HasDatabaseName("ux_securities_isin");
        security.HasOne(entity => entity.Category)
            .WithMany()
            .HasForeignKey(entity => entity.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        var systemDate = modelBuilder.Entity<SystemDateEntity>();
        systemDate.ToTable("system_dates");
        systemDate.HasKey(entity => entity.Key);
        systemDate.Property(entity => entity.Key)
            .HasColumnName("key")
            .HasMaxLength(50);
        systemDate.Property(entity => entity.BusinessDate)
            .HasColumnName("business_date")
            .HasColumnType("date");
    }
}

internal sealed class RfqCaseEntity
{
    public long CaseId { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string SecurityId { get; set; } = string.Empty;

    public string CategorySnapshot { get; set; } = string.Empty;

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

    public string ContactOwnerId { get; set; } = string.Empty;

    public string AssignedTraderId { get; set; } = string.Empty;

    public bool Owned { get; set; }

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

    public DateOnly SettlementDate { get; set; }

    public DateOnly StandardSettlementDate { get; set; }

    public RfqCaseEntity RfqCase { get; set; } = null!;
}
