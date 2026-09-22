using Rfq.Domain;

namespace Rfq.Application;

internal static class ClosedRfqUseCase
{
    public static async Task<RfqCase> LoadAsync(
        IRfqCaseRepository cases,
        CaseId caseId,
        CancellationToken cancellationToken) =>
        await cases.GetAsync(caseId, cancellationToken)
            ?? throw new RfqNotFoundException($"RFQ Case '{caseId}' was not found.");

    public static CloseRfqResult Apply(
        RfqCase rfq,
        Func<RfqCase, CloseTransitionResult> transition,
        RfqTransitionKind eventKind,
        DateOnly businessDate,
        IRfqCaseRepository cases,
        IRfqEventSink events,
        CurrentUser currentUser,
        TimeProvider timeProvider)
    {
        CloseTransitionResult result = transition(rfq);
        rfq = result.Rfq;
        cases.Update(rfq);
        if (result.DiscardedRevision is not null)
        {
            cases.UpdateRevision(result.DiscardedRevision);
        }

        ClosedRfq closed = rfq.Lifecycle as ClosedRfq
            ?? throw new DomainInvariantException("Close result requires a Closed RFQ.");
        events.Record(new RfqTransition(
            eventKind,
            rfq.CaseId,
            currentUser.UserId,
            timeProvider.GetUtcNow(),
            closed.ClosedQuoteId,
            BusinessDate: businessDate));
        return ToResult(rfq);
    }

    public static CloseRfqResult ToResult(RfqCase rfq)
    {
        ClosedRfq closed = rfq.Lifecycle as ClosedRfq
            ?? throw new DomainInvariantException("Close result requires a Closed RFQ.");
        return new CloseRfqResult(
            rfq.CaseId, rfq.Status, closed.ClosedQuoteId, false, rfq.Version);
    }
}
