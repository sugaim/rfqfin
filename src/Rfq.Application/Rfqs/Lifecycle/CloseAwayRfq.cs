using Rfq.Domain;

namespace Rfq.Application;

public sealed class CloseAwayRfq(
    IRfqCaseRepository cases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink events,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<CloseAwayRfqResult> ExecuteAsync(
        CaseId caseId,
        StateVersion expectedCurrentVersion,
        CancellationToken cancellationToken = default)
    {
        RfqCase rfq = await ClosedRfqUseCase.LoadAsync(cases, caseId, cancellationToken);
        if (rfq.Lifecycle is ClosedRfq)
        {
            authorization.EnsureCanCorrectOutcome(currentUser.User, rfq);
            if (rfq.Version != expectedCurrentVersion)
            {
                throw new StateVersionMismatchException("The RFQ Case was changed by another user.");
            }

            if (rfq.Lifecycle is HitRfq)
            {
                throw new DomainRuleViolationException(
                    "A Hit RFQ can only be changed through outcome correction.");
            }

            return new CloseAwayRfqResult(
                CloseAwayOutcome.AlreadyAway, ClosedRfqUseCase.ToResult(rfq));
        }

        authorization.EnsureCanClose(currentUser.User, rfq);
        CloseRfqResult result = await ClosedRfqUseCase.ApplyAsync(
            rfq,
            value => RfqLifecycleTransitions.CloseAway(value, expectedCurrentVersion),
            RfqTransitionKind.ClosedAway,
            cases,
            events,
            unitOfWork,
            currentUser.User,
            timeProvider,
            cancellationToken);
        return new CloseAwayRfqResult(CloseAwayOutcome.ClosedAway, result);
    }
}

public enum CloseAwayOutcome
{
    ClosedAway,
    AlreadyAway
}

public sealed record CloseAwayRfqResult(CloseAwayOutcome Outcome, CloseRfqResult Rfq);
