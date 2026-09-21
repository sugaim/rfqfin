using System.Text.Json;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class PersistedEventSink : IQuoteEventSink, IRfqEventSink
{
    private readonly List<PendingEvent> pending = [];

    internal IReadOnlyList<PendingEvent> Pending => pending;

    public void Record(QuoteTransition transition)
    {
        pending.Add(new PendingQuoteEvent(
            transition.QuoteId,
            transition.Kind,
            transition.PerformedBy,
            transition.OccurredAt,
            JsonSerializer.Serialize(new
            {
                transition.Kind,
                QuoteId = transition.QuoteId.Value,
                PerformedBy = transition.PerformedBy.Value,
                transition.OccurredAt,
            })));
    }

    public void Record(RfqTransition transition)
    {
        pending.Add(new PendingRfqEvent(
            transition.CaseId,
            transition.Kind,
            transition.PerformedBy,
            transition.OccurredAt,
            JsonSerializer.Serialize(new
            {
                transition.Kind,
                CaseId = transition.CaseId.Value,
                PerformedBy = transition.PerformedBy.Value,
                transition.OccurredAt,
                QuoteId = transition.QuoteId?.Value,
                transition.From,
                transition.To,
                transition.Reason,
            })));
    }

    internal void Clear() => pending.Clear();
}

internal abstract record PendingEvent(
    UserId ActorUserId,
    DateTimeOffset OccurredAt,
    string PayloadJson);

internal sealed record PendingRfqEvent(
    CaseId CaseId,
    RfqTransitionKind Type,
    UserId ActorUserId,
    DateTimeOffset OccurredAt,
    string PayloadJson)
    : PendingEvent(ActorUserId, OccurredAt, PayloadJson);

internal sealed record PendingQuoteEvent(
    QuoteId QuoteId,
    QuoteTransitionKind Type,
    UserId ActorUserId,
    DateTimeOffset OccurredAt,
    string PayloadJson)
    : PendingEvent(ActorUserId, OccurredAt, PayloadJson);
