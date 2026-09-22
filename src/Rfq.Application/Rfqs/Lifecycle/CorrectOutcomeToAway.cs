using Rfq.Domain;

namespace Rfq.Application;

public sealed class CorrectOutcomeToAway(
    IRfqCaseRepository cases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink events,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public Task<CloseRfqResult> ExecuteAsync(
        CaseId caseId,
        string? reason,
        StateVersion expectedCurrentVersion,
        CancellationToken cancellationToken = default) =>
        CorrectOutcomeUseCase.ExecuteAsync(
            caseId,
            reason,
            expectedCurrentVersion,
            RfqLifecycleTransitions.CorrectToAway,
            RfqStatus.Hit,
            RfqStatus.Away,
            cases,
            authorization,
            currentUser,
            events,
            unitOfWork,
            timeProvider,
            cancellationToken);
}
