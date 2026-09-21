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

    internal DbSet<WorkingQuoteEntity> WorkingQuotes => Set<WorkingQuoteEntity>();

    internal DbSet<ConfirmedQuoteEntity> ConfirmedQuotes => Set<ConfirmedQuoteEntity>();

    internal DbSet<CaseMemoEntity> CaseMemos => Set<CaseMemoEntity>();

    internal DbSet<CalculationFailureLogEntity> CalculationFailureLogs =>
        Set<CalculationFailureLogEntity>();

    internal DbSet<EventCursorEntity> EventCursors => Set<EventCursorEntity>();
    internal DbSet<EventEntity> Events => Set<EventEntity>();
    internal DbSet<RfqEventEntity> RfqEvents => Set<RfqEventEntity>();
    internal DbSet<QuoteEventEntity> QuoteEvents => Set<QuoteEventEntity>();
    internal DbSet<UserGridConfigEntity> UserGridConfigs => Set<UserGridConfigEntity>();

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
        rfqCase.Property(entity => entity.CopiedFromCaseId)
            .HasColumnName("copied_from_case_id");
        rfqCase.HasOne<RfqCaseEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.CopiedFromCaseId)
            .OnDelete(DeleteBehavior.Restrict);
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
        revision.Property(entity => entity.Notional)
            .HasColumnName("notional")
            .HasPrecision(20, 2);
        revision.Property(entity => entity.SalesAndTradingMessage)
            .HasColumnName("sales_and_trading_message");
        revision.Property(entity => entity.QuoteSeedRevisionId)
            .HasColumnName("quote_seed_revision_id");
        revision.Property(entity => entity.CopiedFromRevisionId)
            .HasColumnName("copied_from_revision_id");
        revision.Property(entity => entity.ConfirmedAt)
            .HasColumnName("confirmed_at")
            .HasColumnType("timestamp with time zone");
        revision.Property(entity => entity.ConfirmedBy)
            .HasColumnName("confirmed_by")
            .HasMaxLength(100);
        revision.HasOne(entity => entity.RfqCase)
            .WithMany(entity => entity.Revisions)
            .HasForeignKey(entity => entity.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
        revision.HasIndex(entity => entity.CaseId)
            .IsUnique()
            .HasFilter("status = 'Draft'")
            .HasDatabaseName("ux_rfq_revisions_one_draft_per_case");
        revision.HasOne<RfqRevisionEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.QuoteSeedRevisionId)
            .OnDelete(DeleteBehavior.Restrict);
        revision.HasOne<RfqRevisionEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.CopiedFromRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

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
        current.Property(entity => entity.QuoteStatus)
            .HasColumnName("quote_status")
            .HasConversion<string>()
            .HasMaxLength(30);
        current.Property(entity => entity.QuoteRequestReason)
            .HasColumnName("quote_request_reason")
            .HasConversion<string>()
            .HasMaxLength(30);
        current.Property(entity => entity.CurrentRevisionId)
            .HasColumnName("current_revision_id");
        current.Property(entity => entity.CurrentQuoteId)
            .HasColumnName("current_quote_id");
        current.Property(entity => entity.ClosedQuoteId)
            .HasColumnName("closed_quote_id");
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
        user.Property(entity => entity.DefaultQuoteExpiryMinutes)
            .HasColumnName("default_quote_expiry_minutes");
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

        var workingQuote = modelBuilder.Entity<WorkingQuoteEntity>();
        workingQuote.ToTable("working_quotes");
        workingQuote.HasKey(entity => entity.RevisionId);
        workingQuote.Property(entity => entity.RevisionId)
            .HasColumnName("revision_id")
            .ValueGeneratedNever();
        workingQuote.Property(entity => entity.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();
        workingQuote.Property(entity => entity.Mode)
            .HasColumnName("mode")
            .HasConversion<string>()
            .HasMaxLength(30);
        workingQuote.Property(entity => entity.CalculatedPayloadJson)
            .HasColumnName("calculated_payload")
            .HasColumnType("jsonb");
        workingQuote.Property(entity => entity.ManualPayloadJson)
            .HasColumnName("manual_payload")
            .HasColumnType("jsonb");
        workingQuote.Property(entity => entity.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone");
        workingQuote.Property(entity => entity.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100);
        workingQuote.Property(entity => entity.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");
        workingQuote.Property(entity => entity.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(100);
        workingQuote.HasOne(entity => entity.Revision)
            .WithOne()
            .HasForeignKey<WorkingQuoteEntity>(entity => entity.RevisionId)
            .OnDelete(DeleteBehavior.Cascade);

        var confirmedQuote = modelBuilder.Entity<ConfirmedQuoteEntity>();
        confirmedQuote.ToTable("confirmed_quotes");
        confirmedQuote.HasKey(entity => entity.QuoteId);
        confirmedQuote.Property(entity => entity.QuoteId)
            .HasColumnName("quote_id")
            .ValueGeneratedNever();
        confirmedQuote.Property(entity => entity.RevisionId)
            .HasColumnName("revision_id");
        confirmedQuote.Property(entity => entity.SecurityId)
            .HasColumnName("security_id")
            .HasMaxLength(100);
        confirmedQuote.Property(entity => entity.SettlementDate)
            .HasColumnName("settlement_date")
            .HasColumnType("date");
        confirmedQuote.Property(entity => entity.ConfirmedBy)
            .HasColumnName("confirmed_by")
            .HasMaxLength(100);
        confirmedQuote.Property(entity => entity.ConfirmedAt)
            .HasColumnName("confirmed_at")
            .HasColumnType("timestamp with time zone");
        confirmedQuote.Property(entity => entity.Mode)
            .HasColumnName("mode")
            .HasConversion<string>()
            .HasMaxLength(30);
        confirmedQuote.Property(entity => entity.CalculatedPayloadJson)
            .HasColumnName("calculated_payload")
            .HasColumnType("jsonb");
        confirmedQuote.Property(entity => entity.ManualPayloadJson)
            .HasColumnName("manual_payload")
            .HasColumnType("jsonb");
        confirmedQuote.Property(entity => entity.ExpiryMinutes)
            .HasColumnName("expiry_minutes");
        confirmedQuote.Property(entity => entity.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamp with time zone");
        confirmedQuote.Property(entity => entity.RequestReasonAnswered)
            .HasColumnName("request_reason_answered")
            .HasConversion<string>()
            .HasMaxLength(30);
        confirmedQuote.HasOne(entity => entity.Revision)
            .WithMany()
            .HasForeignKey(entity => entity.RevisionId)
            .OnDelete(DeleteBehavior.Restrict);
        confirmedQuote.HasIndex(entity => new { entity.RevisionId, entity.ConfirmedAt })
            .HasDatabaseName("ix_confirmed_quotes_revision_id_confirmed_at");

        current.HasOne(entity => entity.CurrentQuote)
            .WithMany()
            .HasForeignKey(entity => entity.CurrentQuoteId)
            .OnDelete(DeleteBehavior.Restrict);
        current.HasOne(entity => entity.ClosedQuote)
            .WithMany()
            .HasForeignKey(entity => entity.ClosedQuoteId)
            .OnDelete(DeleteBehavior.Restrict);

        var caseMemo = modelBuilder.Entity<CaseMemoEntity>();
        caseMemo.ToTable("case_memos");
        caseMemo.HasKey(entity => entity.CaseId);
        caseMemo.Property(entity => entity.CaseId)
            .HasColumnName("case_id")
            .ValueGeneratedNever();
        caseMemo.Property(entity => entity.SalesMemo)
            .HasColumnName("sales_memo");
        caseMemo.Property(entity => entity.TraderMemo)
            .HasColumnName("trader_memo");
        caseMemo.Property(entity => entity.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();
        caseMemo.HasOne(entity => entity.RfqCase)
            .WithOne(entity => entity.Memo)
            .HasForeignKey<CaseMemoEntity>(entity => entity.CaseId)
            .OnDelete(DeleteBehavior.Cascade);

        var calculationFailure = modelBuilder.Entity<CalculationFailureLogEntity>();
        calculationFailure.ToTable("calculation_failure_logs");
        calculationFailure.HasKey(entity => entity.FailureLogId);
        calculationFailure.Property(entity => entity.FailureLogId)
            .HasColumnName("failure_log_id")
            .ValueGeneratedNever();
        calculationFailure.Property(entity => entity.CaseId)
            .HasColumnName("case_id");
        calculationFailure.Property(entity => entity.RevisionId)
            .HasColumnName("revision_id");
        calculationFailure.Property(entity => entity.TraderId)
            .HasColumnName("trader_id")
            .HasMaxLength(100);
        calculationFailure.Property(entity => entity.RequestId)
            .HasColumnName("request_id");
        calculationFailure.Property(entity => entity.Driver)
            .HasColumnName("driver")
            .HasConversion<string>()
            .HasMaxLength(50);
        calculationFailure.Property(entity => entity.AttemptedValue)
            .HasColumnName("attempted_value")
            .HasPrecision(20, 8);
        calculationFailure.Property(entity => entity.SimpleYieldSlide)
            .HasColumnName("simple_yield_slide")
            .HasPrecision(20, 8);
        calculationFailure.Property(entity => entity.PriorWorkingQuoteJson)
            .HasColumnName("prior_working_quote")
            .HasColumnType("jsonb");
        calculationFailure.Property(entity => entity.RequestJson)
            .HasColumnName("request_payload")
            .HasColumnType("jsonb");
        calculationFailure.Property(entity => entity.ErrorCode)
            .HasColumnName("error_code")
            .HasMaxLength(100);
        calculationFailure.Property(entity => entity.ErrorMessage)
            .HasColumnName("error_message");
        calculationFailure.Property(entity => entity.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamp with time zone");
        calculationFailure.HasOne<RfqCaseEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
        calculationFailure.HasOne<RfqRevisionEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.RevisionId)
            .OnDelete(DeleteBehavior.Cascade);
        calculationFailure.HasIndex(entity => new { entity.CaseId, entity.OccurredAt })
            .HasDatabaseName("ix_calculation_failure_logs_case_id_occurred_at");

        var eventCursor = modelBuilder.Entity<EventCursorEntity>();
        eventCursor.ToTable("event_cursors");
        eventCursor.HasKey(entity => entity.CursorKey);
        eventCursor.Property(entity => entity.CursorKey).HasColumnName("cursor_key").HasMaxLength(30);
        eventCursor.Property(entity => entity.LastEventId).HasColumnName("last_event_id");
        eventCursor.HasData(new EventCursorEntity { CursorKey = "global", LastEventId = 0 });

        var eventEntity = modelBuilder.Entity<EventEntity>();
        eventEntity.ToTable("events");
        eventEntity.HasKey(entity => entity.EventId);
        eventEntity.Property(entity => entity.EventId).HasColumnName("event_id").ValueGeneratedNever();
        eventEntity.Property(entity => entity.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone");
        eventEntity.Property(entity => entity.ActorUserId).HasColumnName("actor_user_id").HasMaxLength(100);

        var rfqEvent = modelBuilder.Entity<RfqEventEntity>();
        rfqEvent.ToTable("rfq_events");
        rfqEvent.HasKey(entity => entity.EventId);
        rfqEvent.Property(entity => entity.EventId).HasColumnName("event_id").ValueGeneratedNever();
        rfqEvent.Property(entity => entity.CaseId).HasColumnName("case_id");
        rfqEvent.Property(entity => entity.Type).HasColumnName("type").HasMaxLength(100);
        rfqEvent.Property(entity => entity.PayloadJson).HasColumnName("payload").HasColumnType("jsonb");
        rfqEvent.HasOne(entity => entity.Event).WithOne().HasForeignKey<RfqEventEntity>(entity => entity.EventId).OnDelete(DeleteBehavior.Cascade);
        rfqEvent.HasOne<RfqCaseEntity>().WithMany().HasForeignKey(entity => entity.CaseId).OnDelete(DeleteBehavior.Cascade);

        var quoteEvent = modelBuilder.Entity<QuoteEventEntity>();
        quoteEvent.ToTable("quote_events");
        quoteEvent.HasKey(entity => entity.EventId);
        quoteEvent.Property(entity => entity.EventId).HasColumnName("event_id").ValueGeneratedNever();
        quoteEvent.Property(entity => entity.QuoteId).HasColumnName("quote_id");
        quoteEvent.Property(entity => entity.Type).HasColumnName("type").HasMaxLength(100);
        quoteEvent.Property(entity => entity.PayloadJson).HasColumnName("payload").HasColumnType("jsonb");
        quoteEvent.HasOne(entity => entity.Event).WithOne().HasForeignKey<QuoteEventEntity>(entity => entity.EventId).OnDelete(DeleteBehavior.Cascade);
        quoteEvent.HasOne<ConfirmedQuoteEntity>().WithMany().HasForeignKey(entity => entity.QuoteId).OnDelete(DeleteBehavior.Cascade);

        var gridConfig = modelBuilder.Entity<UserGridConfigEntity>();
        gridConfig.ToTable("user_grid_configs");
        gridConfig.HasKey(entity => new { entity.UserId, entity.ScreenId, entity.ConfigKey });
        gridConfig.Property(entity => entity.UserId).HasColumnName("user_id").HasMaxLength(100);
        gridConfig.Property(entity => entity.ScreenId).HasColumnName("screen_id").HasMaxLength(100);
        gridConfig.Property(entity => entity.ConfigKey).HasColumnName("config_key").HasMaxLength(100);
        gridConfig.Property(entity => entity.Version).HasColumnName("version");
        gridConfig.Property(entity => entity.ConfigJson).HasColumnName("config_json").HasColumnType("jsonb");
        gridConfig.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
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

    public long? CopiedFromCaseId { get; set; }

    public List<RfqRevisionEntity> Revisions { get; set; } = [];

    public CaseCurrentEntity Current { get; set; } = null!;

    public CaseMemoEntity Memo { get; set; } = null!;
}

internal sealed class CaseCurrentEntity
{
    public long CaseId { get; set; }

    public RfqLifecycleKind Lifecycle { get; set; }

    public RfqStatus RfqStatus { get; set; }

    public QuoteStatus? QuoteStatus { get; set; }

    public QuoteRequestReason? QuoteRequestReason { get; set; }

    public Guid CurrentRevisionId { get; set; }

    public Guid? CurrentQuoteId { get; set; }

    public Guid? ClosedQuoteId { get; set; }

    public long Version { get; set; }

    public string ContactOwnerId { get; set; } = string.Empty;

    public string AssignedTraderId { get; set; } = string.Empty;

    public bool Owned { get; set; }

    public RfqCaseEntity RfqCase { get; set; } = null!;

    public RfqRevisionEntity CurrentRevision { get; set; } = null!;

    public ConfirmedQuoteEntity? CurrentQuote { get; set; }

    public ConfirmedQuoteEntity? ClosedQuote { get; set; }
}

internal sealed class CaseMemoEntity
{
    public long CaseId { get; set; }

    public string SalesMemo { get; set; } = string.Empty;

    public string TraderMemo { get; set; } = string.Empty;

    public long Version { get; set; }

    public RfqCaseEntity RfqCase { get; set; } = null!;
}

internal sealed class RfqRevisionEntity
{
    public Guid RevisionId { get; set; }

    public long CaseId { get; set; }

    public RevisionStatus Status { get; set; }

    public long Version { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateOnly? SettlementDate { get; set; }

    public DateOnly StandardSettlementDate { get; set; }

    public decimal? Notional { get; set; }

    public string SalesAndTradingMessage { get; set; } = string.Empty;

    public Guid? QuoteSeedRevisionId { get; set; }

    public Guid? CopiedFromRevisionId { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public string? ConfirmedBy { get; set; }

    public RfqCaseEntity RfqCase { get; set; } = null!;
}

internal sealed class WorkingQuoteEntity
{
    public Guid RevisionId { get; set; }

    public long Version { get; set; }

    public WorkingQuoteMode Mode { get; set; }

    public string? CalculatedPayloadJson { get; set; }

    public string? ManualPayloadJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;

    public RfqRevisionEntity Revision { get; set; } = null!;
}

internal sealed class ConfirmedQuoteEntity
{
    public Guid QuoteId { get; set; }
    public Guid RevisionId { get; set; }
    public string SecurityId { get; set; } = string.Empty;
    public DateOnly SettlementDate { get; set; }
    public string ConfirmedBy { get; set; } = string.Empty;
    public DateTimeOffset ConfirmedAt { get; set; }
    public WorkingQuoteMode Mode { get; set; }
    public string? CalculatedPayloadJson { get; set; }
    public string? ManualPayloadJson { get; set; }
    public int? ExpiryMinutes { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public QuoteRequestReason RequestReasonAnswered { get; set; }
    public RfqRevisionEntity Revision { get; set; } = null!;
}

internal sealed class CalculationFailureLogEntity
{
    public Guid FailureLogId { get; set; }

    public long CaseId { get; set; }

    public Guid RevisionId { get; set; }

    public string TraderId { get; set; } = string.Empty;

    public Guid RequestId { get; set; }

    public CalculationDriver Driver { get; set; }

    public decimal AttemptedValue { get; set; }

    public decimal SimpleYieldSlide { get; set; }

    public string PriorWorkingQuoteJson { get; set; } = "{}";

    public string RequestJson { get; set; } = "{}";

    public string ErrorCode { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }
}

internal sealed class EventCursorEntity
{
    public string CursorKey { get; set; } = string.Empty;
    public long LastEventId { get; set; }
}

internal sealed class EventEntity
{
    public long EventId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? ActorUserId { get; set; }
}

internal sealed class RfqEventEntity
{
    public long EventId { get; set; }
    public long CaseId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public EventEntity Event { get; set; } = null!;
}

internal sealed class QuoteEventEntity
{
    public long EventId { get; set; }
    public Guid QuoteId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public EventEntity Event { get; set; } = null!;
}

internal sealed class UserGridConfigEntity
{
    public string UserId { get; set; } = string.Empty;
    public string ScreenId { get; set; } = string.Empty;
    public string ConfigKey { get; set; } = string.Empty;
    public int Version { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; }
}
