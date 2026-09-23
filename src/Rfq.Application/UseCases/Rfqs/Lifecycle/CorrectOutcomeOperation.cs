using Rfq.Domain;

namespace Rfq.Application;

public sealed class CorrectOutcomeOperation(
    IRfqCaseRepository cases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink events,
    IBusinessDateProvider businessDateProvider,
    TimeProvider timeProvider)
{
    public async Task<CloseRfqResult> ApplyAsync(
        CaseId caseId,
        string? reason,
        StateVersion expectedCurrentVersion,
        RfqStatus targetOutcome,
        CancellationToken cancellationToken)
    {
        string normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? throw new RfqRequestValidationException(
                "Correction Reason is required.")
            : reason.Trim();
        RfqCase rfq = await ClosedRfqUseCase.LoadAsync(
            cases,
            caseId,
            cancellationToken);
        authorization.EnsureCanCorrectOutcome(currentUser.User, rfq);
        ClosedRfq closed = rfq.Lifecycle as ClosedRfq
            ?? throw new DomainInvariantException(
                "Outcome correction requires a Closed RFQ.");
        DateOnly businessDate = await businessDateProvider.GetCurrentAsync(
            cancellationToken);
        if (closed.ClosedBusinessDate != businessDate)
        {
            throw new DomainRuleViolationException(
                "Outcome correction is only allowed on the original close Business Date.");
        }

        RfqStatus from = rfq.Status;
        rfq = targetOutcome switch
        {
            RfqStatus.Hit => RfqLifecycleTransitions.CorrectToHit(
                rfq,
                expectedCurrentVersion),
            RfqStatus.Away => RfqLifecycleTransitions.CorrectToAway(
                rfq,
                expectedCurrentVersion),
            _ => throw new RfqRequestValidationException(
                "Corrected outcome must be Hit or Away."),
        };
        cases.Update(rfq);
        events.Record(new RfqTransition(
            RfqTransitionKind.OutcomeCorrected,
            caseId,
            currentUser.User.UserId,
            timeProvider.GetUtcNow(),
            rfq.ClosedQuoteId
                ?? throw new DomainInvariantException(
                    "Corrected RFQ is not Closed."),
            from.ToString(),
            targetOutcome.ToString(),
            normalizedReason,
            businessDate));
        return ClosedRfqUseCase.ToResult(rfq);
    }
}
