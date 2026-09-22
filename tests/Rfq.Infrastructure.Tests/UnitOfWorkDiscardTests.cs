using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;
using Xunit;

namespace Rfq.Infrastructure.Tests;

public sealed class UnitOfWorkDiscardTests
{
    [Fact]
    public void Discard_changes_clears_tracked_mutations_and_pending_events()
    {
        DbContextOptions<RfqDbContext> options = new DbContextOptionsBuilder<RfqDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;
        using var context = new RfqDbContext(options);
        var sink = new PersistedEventSink();
        context.SeedMarkers.Add(new SeedMarker("pending", DateTimeOffset.UtcNow));
        sink.Record(new RfqTransition(
            RfqTransitionKind.Cancelled,
            new CaseId(1),
            UserId.Create("sales"),
            DateTimeOffset.UtcNow,
            BusinessDate: new DateOnly(2026, 9, 21)));
        var unitOfWork = new PostgreSqlUnitOfWork(context, sink);

        unitOfWork.DiscardChanges();

        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Empty(sink.Pending);
    }
}
