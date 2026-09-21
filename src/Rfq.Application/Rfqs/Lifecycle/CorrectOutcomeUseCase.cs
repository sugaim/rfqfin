using Rfq.Domain;

namespace Rfq.Application;

internal static class CorrectOutcomeUseCase
{
    public static async Task<CloseRfqResult> ExecuteAsync(
        CaseId caseId,
        string? reason,
        StateVersion expectedCurrentVersion,
        Func<RfqCase, StateVersion, RfqCase> transition,
        RfqStatus from,
        RfqStatus to,
        IRfqCaseRepository cases,
        IRfqAuthorization authorization,
        ICurrentUser currentUser,
        IRfqEventSink events,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var rfq = await ClosedRfqUseCase.LoadAsync(cases, caseId, cancellationToken);
        authorization.EnsureCanCorrectOutcome(currentUser.User, rfq);
        rfq = transition(rfq, expectedCurrentVersion);
        cases.Update(rfq);
        events.Record(new RfqTransition(
            RfqTransitionKind.OutcomeCorrected, caseId, currentUser.User.UserId,
            timeProvider.GetUtcNow(), rfq.ClosedQuoteId
                ?? throw new DomainInvariantException("Corrected RFQ is not Closed."),
            from.ToString(), to.ToString(),
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ClosedRfqUseCase.ToResult(rfq);
    }
}
