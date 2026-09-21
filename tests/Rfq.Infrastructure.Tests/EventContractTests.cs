using Rfq.Application;
using Rfq.Domain;
using Xunit;

namespace Rfq.Infrastructure.Tests;

public sealed class EventContractTests
{
    [Fact]
    public void Pending_events_use_target_specific_typed_identifiers()
    {
        var sink = new PersistedEventSink();
        var actor = UserId.Create("sales-dev");
        var caseId = new CaseId(42);
        var quoteId = QuoteId.New();
        var now = DateTimeOffset.UtcNow;

        sink.Record(new RfqTransition(
            RfqTransitionKind.RevisionConfirmed, caseId, actor, now));
        sink.Record(new QuoteTransition(
            QuoteTransitionKind.Confirmed, quoteId, actor, now));

        var rfqEvent = Assert.IsType<PendingRfqEvent>(sink.Pending[0]);
        Assert.Equal(caseId, rfqEvent.CaseId);
        Assert.Equal(actor, rfqEvent.ActorUserId);
        var quoteEvent = Assert.IsType<PendingQuoteEvent>(sink.Pending[1]);
        Assert.Equal(quoteId, quoteEvent.QuoteId);
        Assert.Equal(actor, quoteEvent.ActorUserId);
    }

    [Fact]
    public void Persisted_event_types_parse_strictly_from_database_strings()
    {
        Assert.Equal(
            RfqTransitionKind.RevisionConfirmed,
            PersistedEventTypeParser.ParseRfq("RevisionConfirmed"));
        Assert.Equal(
            QuoteTransitionKind.Confirmed,
            PersistedEventTypeParser.ParseQuote("Confirmed"));

        Assert.Throws<DomainInvariantException>(
            () => PersistedEventTypeParser.ParseRfq("Unknown"));
        Assert.Throws<DomainInvariantException>(
            () => PersistedEventTypeParser.ParseQuote("999"));
    }
}
