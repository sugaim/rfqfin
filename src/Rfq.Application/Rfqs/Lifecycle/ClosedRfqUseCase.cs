using Rfq.Domain;

namespace Rfq.Application;

internal static class ClosedRfqUseCase
{
    public static async Task<RfqCase> LoadAsync(
        IRfqCaseRepository cases,
        CaseId caseId,
        CancellationToken cancellationToken) =>
        await cases.GetAsync(caseId, cancellationToken)
            ?? throw new KeyNotFoundException($"RFQ Case '{caseId}' was not found.");

    public static async Task<CloseRfqResult> ApplyAsync(
        RfqCase rfq,
        Func<RfqCase, CloseTransitionResult> transition,
        RfqTransitionKind eventKind,
        IRfqCaseRepository cases,
        IRfqEventSink events,
        IUnitOfWork unitOfWork,
        CurrentUser currentUser,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var result = transition(rfq);
        rfq = result.Rfq;
        cases.Update(rfq);
        if (result.DiscardedRevision is not null)
            cases.UpdateRevision(result.DiscardedRevision);
        var closed = rfq.Lifecycle as ClosedRfq
            ?? throw new DomainInvariantException("Close result requires a Closed RFQ.");
        events.Record(new RfqTransition(eventKind, rfq.CaseId, currentUser.UserId,
            timeProvider.GetUtcNow(), closed.ClosedQuoteId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResult(rfq);
    }

    public static CloseRfqResult ToResult(RfqCase rfq)
    {
        var closed = rfq.Lifecycle as ClosedRfq
            ?? throw new DomainInvariantException("Close result requires a Closed RFQ.");
        return new CloseRfqResult(
            rfq.CaseId, rfq.Status, closed.ClosedQuoteId, false, rfq.Version);
    }
}
