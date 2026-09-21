using Rfq.Domain;

namespace Rfq.Application;

public sealed class WithdrawQuote(
    IRfqCaseRepository cases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IQuoteEventSink events,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<LifecycleResult> ExecuteAsync(CaseId caseId, StateVersion expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var rfq = await CloseRfq.LoadAsync(cases, caseId, cancellationToken);
        authorization.EnsureCanWithdraw(currentUser.User, rfq);
        var quoteId = rfq.CurrentQuoteId
            ?? throw new InvalidOperationException("Current quote was not found.");
        rfq = QuoteTransitions.Withdraw(rfq, expectedVersion);
        cases.Update(rfq);
        events.Record(new(QuoteTransitionKind.Withdrawn, quoteId,
            currentUser.User.UserId, timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResult(rfq);
    }

    internal static LifecycleResult ToResult(RfqCase rfq) => new(
        rfq.CaseId, rfq.Status, rfq.QuoteStatus,
        rfq.QuoteRequestReason, rfq.Version);
}
