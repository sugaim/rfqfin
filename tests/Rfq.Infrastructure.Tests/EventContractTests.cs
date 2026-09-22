using System.Text.Json;
using Rfq.Application;
using Rfq.Domain;
using Xunit;

namespace Rfq.Infrastructure.Tests;

public sealed class EventContractTests
{
    private static readonly UserId Actor = UserId.Create("sales-dev");
    private static readonly DateTimeOffset Now = new(2026, 9, 21, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    public void Typed_event_writes_use_stable_code_and_event_specific_payload_only()
    {
        var sink = new PersistedEventSink();
        var quoteId = QuoteId.New();
        sink.Record(new RfqTransition(
            RfqTransitionKind.ClosedHit,
            new CaseId(42),
            Actor,
            Now,
            quoteId));

        PendingRfqClosedHitEvent pending = Assert.IsType<PendingRfqClosedHitEvent>(Assert.Single(sink.Pending));
        EventPersistenceData persisted = EventPersistenceContract.Serialize(pending);

        Assert.Equal(EventPersistenceTypeCodes.Rfq.ClosedHit, persisted.TypeCode);
        using var payload = JsonDocument.Parse(persisted.PayloadJson);
        Assert.Equal(quoteId.Value, payload.RootElement.GetProperty("quoteId").GetGuid());
        Assert.Equal(["quoteId"], [.. payload.RootElement.EnumerateObject().Select(property => property.Name)]);
    }

    [Fact]
    public void Persisted_type_and_payload_map_to_typed_application_event()
    {
        var quoteId = QuoteId.New();
        RfqEvent item = EventPersistenceContract.DeserializeRfq(
            7,
            Now,
            Actor,
            new CaseId(42),
            EventPersistenceTypeCodes.Rfq.ClosedHit,
            $"{{\"quoteId\":\"{quoteId.Value}\"}}");

        RfqClosedHitEvent closed = Assert.IsType<RfqClosedHitEvent>(item);
        Assert.Equal(new CaseId(42), closed.CaseId);
        Assert.Equal(quoteId, closed.QuoteId);
        Assert.Equal(Actor, closed.ActorUserId);
    }

    [Fact]
    public void Quote_type_maps_to_typed_application_event()
    {
        var quoteId = QuoteId.New();
        QuoteEvent item = EventPersistenceContract.DeserializeQuote(
            9,
            Now,
            Actor,
            new CaseId(42),
            quoteId,
            EventPersistenceTypeCodes.Quote.Presented,
            "{}");

        Assert.IsType<QuotePresentedEvent>(item);
    }

    [Theory]
    [InlineData("Unknown", "{}")]
    [InlineData("ClosedHit", "not-json")]
    [InlineData("ClosedHit", "{}")]
    [InlineData("ClosedHit", "{\"quoteId\":\"00000000-0000-0000-0000-000000000000\"}")]
    [InlineData("ClosedHit", "{\"quoteId\":\"14dc46dc-f7eb-4d92-9518-ef88f5eb7cc1\",\"caseId\":42}")]
    public void Invalid_persisted_rfq_event_fails_fast(string type, string payload)
    {
        Assert.Throws<DomainInvariantException>(() => EventPersistenceContract.DeserializeRfq(
            1, Now, Actor, new CaseId(42), type, payload));
    }

    [Fact]
    public void Eod_codes_are_the_same_stable_contract_used_by_event_writes()
    {
        var sink = new PersistedEventSink();
        sink.Record(new RfqTransition(
            RfqTransitionKind.ClosedAway,
            new CaseId(42),
            Actor,
            Now,
            QuoteId.New()));

        EventPersistenceData persisted = EventPersistenceContract.Serialize(Assert.Single(sink.Pending));

        Assert.Equal(EventPersistenceTypeCodes.Rfq.ClosedAway, persisted.TypeCode);
        Assert.Equal("ClosedHit", EventPersistenceTypeCodes.Rfq.ClosedHit);
        Assert.Equal("ClosedAway", EventPersistenceTypeCodes.Rfq.ClosedAway);
    }
}
