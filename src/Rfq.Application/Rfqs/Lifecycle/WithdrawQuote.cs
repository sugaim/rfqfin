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
    public async Task<LifecycleResult> ExecuteAsync(long caseId, long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var rfq = await CloseRfq.LoadAsync(cases, caseId, cancellationToken);
        authorization.EnsureCanWithdraw(currentUser.User, rfq);
        var quoteId = rfq.CurrentQuoteId
            ?? throw new InvalidOperationException("Current quote was not found.");
        rfq = QuoteTransitions.Withdraw(rfq, new StateVersion(expectedVersion));
        cases.Update(rfq);
        events.Record(new(QuoteTransitionKind.Withdrawn, quoteId.Value,
            currentUser.User.UserId.Value, timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResult(rfq);
    }

    internal static LifecycleResult ToResult(RfqCase rfq) => new(
        rfq.CaseId.Value, rfq.Status.ToString(), rfq.QuoteStatus?.ToString(),
        rfq.QuoteRequestReason?.ToString(), rfq.Version.Value);
}
