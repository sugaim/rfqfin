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
    public async Task<WithdrawQuoteResult> ExecuteAsync(
        CaseId caseId,
        StateVersion expectedVersion,
        CancellationToken cancellationToken = default)
    {
        RfqCase rfq = await ClosedRfqUseCase.LoadAsync(cases, caseId, cancellationToken);
        authorization.EnsureCanWithdraw(currentUser.User, rfq);
        if (rfq.Version != expectedVersion)
        {
            throw new StateVersionMismatchException("The RFQ Case was changed by another user.");
        }

        if (rfq.Lifecycle is ActiveRfq { QuoteState: QuoteRequested })
        {
            return new WithdrawQuoteResult(WithdrawQuoteOutcome.AlreadyRequested, ToResult(rfq));
        }

        QuoteId quoteId = rfq.CurrentQuoteId
            ?? throw new RfqInvariantException("Current quote was not found.");
        rfq = QuoteTransitions.Withdraw(rfq, expectedVersion);
        cases.Update(rfq);
        events.Record(new(
            QuoteTransitionKind.Withdrawn,
            quoteId,
            currentUser.User.UserId,
            timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new WithdrawQuoteResult(WithdrawQuoteOutcome.Withdrawn, ToResult(rfq));
    }

    internal static LifecycleResult ToResult(RfqCase rfq) => new(
        rfq.CaseId,
        rfq.Status,
        rfq.QuoteStatus,
        rfq.QuoteRequestReason,
        rfq.Version);
}

public enum WithdrawQuoteOutcome
{
    Withdrawn,
    AlreadyRequested
}

public sealed record WithdrawQuoteResult(WithdrawQuoteOutcome Outcome, LifecycleResult Rfq);
