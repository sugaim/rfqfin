using Rfq.Domain;

namespace Rfq.Application;

public sealed class BulkCloseRfqs(
    IRfqCaseRepository rfqCases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink eventSink,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<BulkCloseItemResult>> ExecuteAsync(
        IReadOnlyList<BulkCloseItem> items,
        RfqStatus outcome,
        CancellationToken cancellationToken = default)
    {
        if (outcome is not RfqStatus.Hit and not RfqStatus.Away)
        {
            throw new ArgumentException("Bulk close outcome must be Hit or Away.", nameof(outcome));
        }

        var results = new List<BulkCloseItemResult>(items.Count);
        foreach (var item in items)
        {
            try
            {
                var rfqCase = await CloseRfq.LoadAsync(rfqCases, item.CaseId, cancellationToken);
                if (rfqCase.Status is RfqStatus.Hit or RfqStatus.Away)
                {
                    results.Add(new BulkCloseItemResult(
                        item.CaseId,
                        "Skipped",
                        rfqCase.Status,
                        null));
                    continue;
                }

                authorization.EnsureCanClose(currentUser.User, rfqCase);
                var transition = RfqLifecycleTransitions.Close(
                    rfqCase, outcome, item.ExpectedCurrentVersion);
                rfqCase = transition.Rfq;
                rfqCases.Update(rfqCase);
                if (transition.DiscardedRevision is not null)
                    rfqCases.UpdateRevision(transition.DiscardedRevision);
                CloseRfq.RecordClosed(
                    eventSink,
                    currentUser.User,
                    timeProvider,
                    rfqCase,
                    outcome);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                results.Add(new BulkCloseItemResult(
                    item.CaseId,
                    "Closed",
                    outcome,
                    null));
            }
            catch (Exception exception) when (IsExpected(exception))
            {
                results.Add(new BulkCloseItemResult(
                    item.CaseId,
                    "Failed",
                    null,
                    exception.Message));
            }
        }

        return results;
    }

    private static bool IsExpected(Exception exception) => exception is
        ArgumentException or KeyNotFoundException or InvalidOperationException
        or UnauthorizedAccessException or DomainRuleViolationException
        or DomainValidationException or StateVersionMismatchException;
}
