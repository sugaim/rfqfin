using Rfq.Domain;

namespace Rfq.Application;

public sealed record LifecycleResult(
    long CaseId,
    string RfqStatus,
    string? QuoteStatus,
    string? QuoteRequestReason,
    long CurrentVersion);

public sealed record LifecycleItem(long CaseId, long ExpectedCurrentVersion);
public sealed record LifecycleItemResult(long CaseId, string Result, string? Error);

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

public sealed class BulkWithdrawQuotes(WithdrawQuote withdraw)
{
    public async Task<IReadOnlyList<LifecycleItemResult>> ExecuteAsync(
        IReadOnlyList<LifecycleItem> items,
        CancellationToken cancellationToken = default)
    {
        var results = new List<LifecycleItemResult>();
        foreach (var item in items)
        {
            try
            {
                await withdraw.ExecuteAsync(item.CaseId, item.ExpectedCurrentVersion, cancellationToken);
                results.Add(new(item.CaseId, "Withdrawn", null));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                or UnauthorizedAccessException or KeyNotFoundException
                or DomainRuleViolationException or DomainValidationException
                or StateVersionMismatchException)
            {
                results.Add(new(item.CaseId,
                    ex.Message.Contains("Presented", StringComparison.Ordinal) ? "Skipped" : "Failed",
                    ex.Message));
            }
        }
        return results;
    }
}

public sealed class CancelRfq(
    IRfqCaseRepository cases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink events,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<LifecycleResult> ExecuteAsync(long caseId, long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var rfq = await CloseRfq.LoadAsync(cases, caseId, cancellationToken);
        authorization.EnsureCanCancelOrReopen(currentUser.User, rfq);
        rfq = RfqLifecycleTransitions.Cancel(rfq, new StateVersion(expectedVersion));
        cases.Update(rfq);
        events.Record(new(RfqTransitionKind.Cancelled, caseId,
            currentUser.User.UserId.Value, timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WithdrawQuote.ToResult(rfq);
    }
}

public sealed class ReopenRfq(
    IRfqCaseRepository cases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink events,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<LifecycleResult> ExecuteAsync(long caseId, long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var rfq = await CloseRfq.LoadAsync(cases, caseId, cancellationToken);
        authorization.EnsureCanCancelOrReopen(currentUser.User, rfq);
        rfq = RfqLifecycleTransitions.Reopen(rfq, new StateVersion(expectedVersion));
        cases.Update(rfq);
        events.Record(new(RfqTransitionKind.Reopened, caseId,
            currentUser.User.UserId.Value, timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WithdrawQuote.ToResult(rfq);
    }
}

public sealed class ExpireQuote(
    IRfqCaseRepository cases,
    IQuoteEventSink events,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<bool> ExecuteAsync(ExpiredQuoteCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        var rfq = await cases.GetAsync(new CaseId(candidate.CaseId), cancellationToken);
        if (rfq is null)
        {
            return false;
        }
        try
        {
            rfq = QuoteTransitions.Expire(
                rfq, new QuoteId(candidate.QuoteId),
                new StateVersion(candidate.CurrentVersion));
        }
        catch (DomainRuleViolationException)
        {
            return false;
        }
        cases.Update(rfq);
        events.Record(new(QuoteTransitionKind.Expired, candidate.QuoteId,
            "system", timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
