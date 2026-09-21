using Microsoft.EntityFrameworkCore;

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
    internal DbSet<BusinessDateEntity> BusinessDates => Set<BusinessDateEntity>();
    internal DbSet<WorkingQuoteEntity> WorkingQuotes => Set<WorkingQuoteEntity>();
    internal DbSet<ConfirmedQuoteEntity> ConfirmedQuotes => Set<ConfirmedQuoteEntity>();
    internal DbSet<SalesMemoEntity> SalesMemos => Set<SalesMemoEntity>();
    internal DbSet<TraderMemoEntity> TraderMemos => Set<TraderMemoEntity>();
    internal DbSet<CalculationFailureLogEntity> CalculationFailureLogs =>
        Set<CalculationFailureLogEntity>();
    internal DbSet<EventCursorEntity> EventCursors => Set<EventCursorEntity>();
    internal DbSet<EventEntity> Events => Set<EventEntity>();
    internal DbSet<RfqEventEntity> RfqEvents => Set<RfqEventEntity>();
    internal DbSet<QuoteEventEntity> QuoteEvents => Set<QuoteEventEntity>();
    internal DbSet<UserGridConfigEntity> UserGridConfigs => Set<UserGridConfigEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        PostgreSqlModelConfiguration.Apply(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RfqDbContext).Assembly);
    }
}
