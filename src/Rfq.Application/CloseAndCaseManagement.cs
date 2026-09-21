using Rfq.Domain;

namespace Rfq.Application;

public enum RfqTransitionKind
{
    ClosedHit,
    ClosedAway,
    OutcomeCorrected,
    ContactOwnerChanged,
    RevisionConfirmed,
    Cancelled,
    Reopened,
    PickedUp,
    Released,
    AssignedTraderChanged,
    TakenOver,
}

public sealed record RfqTransition(
    RfqTransitionKind Kind,
    long CaseId,
    string PerformedBy,
    DateTimeOffset OccurredAt,
    Guid? QuoteId = null,
    string? From = null,
    string? To = null,
    string? Reason = null);

public interface IRfqEventSink
{
    void Record(RfqTransition transition);
}

public interface ICaseMemoRepository
{
    Task<CaseMemo?> GetAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default);

    void Update(CaseMemo memo);
}

public sealed record CloseRfqResult(
    long CaseId,
    string RfqStatus,
    Guid ClosedQuoteId,
    bool Owned,
    long CurrentVersion);

public sealed record BulkCloseItem(
    long CaseId,
    long ExpectedCurrentVersion);

public sealed record BulkCloseItemResult(
    long CaseId,
    string Result,
    string? RfqStatus,
    string? Error);

public sealed record ContactOwnerResult(
    long CaseId,
    string ContactOwnerId,
    long CurrentVersion);

public sealed record CaseMemoResult(
    long CaseId,
    string Memo,
    long Version);

public sealed class CloseRfq(
    IRfqCaseRepository rfqCases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink eventSink,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<CloseRfqResult> ExecuteAsync(
        long caseId,
        RfqStatus outcome,
        long expectedCurrentVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanClose(currentUser.User, rfqCase);
        rfqCase.Close(outcome, expectedCurrentVersion);
        rfqCases.Update(rfqCase);
        RecordClosed(eventSink, currentUser.User, timeProvider, rfqCase, outcome);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResult(rfqCase);
    }

    internal static async Task<RfqCase> LoadAsync(
        IRfqCaseRepository rfqCases,
        long caseId,
        CancellationToken cancellationToken) =>
        await rfqCases.GetAsync(new CaseId(caseId), cancellationToken)
            ?? throw new KeyNotFoundException($"RFQ Case '{caseId}' was not found.");

    internal static CloseRfqResult ToResult(RfqCase rfqCase) => new(
        rfqCase.CaseId.Value,
        rfqCase.Status.ToString(),
        rfqCase.ClosedQuoteId!.Value.Value,
        rfqCase.Owned,
        rfqCase.CurrentVersion);

    internal static void RecordClosed(
        IRfqEventSink eventSink,
        CurrentUser user,
        TimeProvider timeProvider,
        RfqCase rfqCase,
        RfqStatus outcome) => eventSink.Record(new RfqTransition(
            outcome == RfqStatus.Hit
                ? RfqTransitionKind.ClosedHit
                : RfqTransitionKind.ClosedAway,
            rfqCase.CaseId.Value,
            user.UserId.Value,
            timeProvider.GetUtcNow(),
            rfqCase.ClosedQuoteId!.Value.Value));
}

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
                        rfqCase.Status.ToString(),
                        null));
                    continue;
                }

                authorization.EnsureCanClose(currentUser.User, rfqCase);
                rfqCase.Close(outcome, item.ExpectedCurrentVersion);
                rfqCases.Update(rfqCase);
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
                    outcome.ToString(),
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
        or UnauthorizedAccessException;
}

public sealed class CorrectRfqOutcome(
    IRfqCaseRepository rfqCases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink eventSink,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<CloseRfqResult> ExecuteAsync(
        long caseId,
        RfqStatus outcome,
        string? reason,
        long expectedCurrentVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await CloseRfq.LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanCorrectOutcome(currentUser.User, rfqCase);
        var previous = rfqCase.Status;
        rfqCase.CorrectOutcome(outcome, expectedCurrentVersion);
        rfqCases.Update(rfqCase);
        eventSink.Record(new RfqTransition(
            RfqTransitionKind.OutcomeCorrected,
            caseId,
            currentUser.User.UserId.Value,
            timeProvider.GetUtcNow(),
            rfqCase.ClosedQuoteId!.Value.Value,
            previous.ToString(),
            outcome.ToString(),
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CloseRfq.ToResult(rfqCase);
    }
}

public sealed class ChangeContactOwner(
    IRfqCaseRepository rfqCases,
    IUserDirectory users,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink eventSink,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<ContactOwnerResult> ExecuteAsync(
        long caseId,
        string targetUserId,
        long expectedCurrentVersion,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            throw new ArgumentException("Contact Owner handoff requires confirmation.", nameof(confirmed));
        }

        var rfqCase = await CloseRfq.LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanChangeContactOwner(currentUser.User, rfqCase);
        var targetId = UserId.Create(targetUserId);
        var target = await users.ResolveAsync(targetId, cancellationToken)
            ?? throw new ArgumentException($"User '{targetUserId}' was not found.", nameof(targetUserId));
        if (target.DeskId != currentUser.User.DeskId
            || !target.Roles.Any(role => role is UserRole.Sales or UserRole.Trader))
        {
            throw new ArgumentException(
                "Contact Owner must be a Sales or Trader user on the same desk.",
                nameof(targetUserId));
        }

        var previous = rfqCase.ContactOwnerId.Value;
        rfqCase.ChangeContactOwner(targetId, expectedCurrentVersion);
        rfqCases.Update(rfqCase);
        eventSink.Record(new RfqTransition(
            RfqTransitionKind.ContactOwnerChanged,
            caseId,
            currentUser.User.UserId.Value,
            timeProvider.GetUtcNow(),
            From: previous,
            To: targetId.Value));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ContactOwnerResult(caseId, targetId.Value, rfqCase.CurrentVersion);
    }
}

public sealed class UpdateSalesMemo(
    IRfqCaseRepository rfqCases,
    ICaseMemoRepository memos,
    IUserDirectory users,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<CaseMemoResult> ExecuteAsync(
        long caseId,
        string? memo,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        authorization.EnsureCanUpdateSalesMemo(currentUser.User);
        var rfqCase = await CloseRfq.LoadAsync(rfqCases, caseId, cancellationToken);
        await EnsureDeskAccessAsync(users, currentUser.User, rfqCase, cancellationToken);
        var caseMemo = await GetMemoAsync(memos, caseId, cancellationToken);
        caseMemo.UpdateSales(memo, expectedVersion);
        memos.Update(caseMemo);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CaseMemoResult(caseId, caseMemo.SalesMemo, caseMemo.Version);
    }

    internal static async Task<CaseMemo> GetMemoAsync(
        ICaseMemoRepository memos,
        long caseId,
        CancellationToken cancellationToken) =>
        await memos.GetAsync(new CaseId(caseId), cancellationToken)
            ?? throw new KeyNotFoundException($"Case Memo for RFQ Case '{caseId}' was not found.");

    internal static async Task EnsureDeskAccessAsync(
        IUserDirectory users,
        CurrentUser user,
        RfqCase rfqCase,
        CancellationToken cancellationToken)
    {
        var assignedTrader = await users.ResolveAsync(
            rfqCase.AssignedTraderId,
            cancellationToken);
        if (assignedTrader is null || assignedTrader.DeskId != user.DeskId)
        {
            throw new UnauthorizedAccessException(
                "The RFQ Case is outside the current user's desk scope.");
        }
    }
}

public sealed class UpdateTraderMemo(
    IRfqCaseRepository rfqCases,
    ICaseMemoRepository memos,
    IUserDirectory users,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<CaseMemoResult> ExecuteAsync(
        long caseId,
        string? memo,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        authorization.EnsureCanUpdateTraderMemo(currentUser.User);
        var rfqCase = await CloseRfq.LoadAsync(rfqCases, caseId, cancellationToken);
        await UpdateSalesMemo.EnsureDeskAccessAsync(
            users,
            currentUser.User,
            rfqCase,
            cancellationToken);
        var caseMemo = await UpdateSalesMemo.GetMemoAsync(
            memos,
            caseId,
            cancellationToken);
        caseMemo.UpdateTrader(memo, expectedVersion);
        memos.Update(caseMemo);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CaseMemoResult(caseId, caseMemo.TraderMemo, caseMemo.Version);
    }
}
