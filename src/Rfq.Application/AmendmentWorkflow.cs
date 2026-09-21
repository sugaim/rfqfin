using Rfq.Domain;

namespace Rfq.Application;

public sealed record SaveAmendmentCommand(
    long CaseId,
    decimal? Notional,
    DateOnly? SettlementDate,
    string? SalesAndTradingMessage,
    long ExpectedCurrentVersion,
    long? ExpectedDraftVersion);

public sealed record AmendmentResult(
    long CaseId,
    Guid CurrentRevisionId,
    Guid? DraftRevisionId,
    long CurrentVersion,
    long? DraftVersion,
    string RfqStatus,
    string? QuoteStatus,
    string? QuoteRequestReason);

public sealed record AmendmentItem(long CaseId, long ExpectedCurrentVersion, long ExpectedDraftVersion);
public sealed record AmendmentItemResult(long CaseId, string Result, string? Error);

public sealed class SaveAmendment(
    IRfqCaseRepository cases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<AmendmentResult> ExecuteAsync(
        SaveAmendmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var rfq = await CloseRfq.LoadAsync(cases, command.CaseId, cancellationToken);
        authorization.EnsureCanEditRevision(currentUser.User, rfq);
        rfq.SaveAmendmentDraft(
            command.Notional,
            command.SettlementDate,
            command.SalesAndTradingMessage,
            currentUser.User.UserId,
            timeProvider.GetUtcNow(),
            command.ExpectedCurrentVersion,
            command.ExpectedDraftVersion);
        cases.Update(rfq);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResult(rfq);
    }

    internal static AmendmentResult ToResult(RfqCase rfq) => new(
        rfq.CaseId.Value,
        rfq.CurrentRevision.RevisionId.Value,
        rfq.PendingDraftRevision?.RevisionId.Value,
        rfq.CurrentVersion,
        rfq.PendingDraftRevision?.Version,
        rfq.Status.ToString(),
        rfq.QuoteStatus?.ToString(),
        rfq.QuoteRequestReason?.ToString());
}

public sealed class ConfirmAmendment(
    IRfqCaseRepository cases,
    IWorkingQuoteEnsurer workingQuotes,
    ISystemDateProvider systemDate,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IRfqEventSink events,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<AmendmentResult> ExecuteAsync(
        AmendmentItem command,
        CancellationToken cancellationToken = default)
    {
        var rfq = await CloseRfq.LoadAsync(cases, command.CaseId, cancellationToken);
        authorization.EnsureCanConfirmRevision(currentUser.User, rfq);
        var now = timeProvider.GetUtcNow();
        rfq.ConfirmAmendment(
            await systemDate.GetTodayAsync(cancellationToken),
            currentUser.User.UserId,
            now,
            command.ExpectedCurrentVersion,
            command.ExpectedDraftVersion);
        cases.Update(rfq);
        await workingQuotes.EnsureAsync(
            rfq.CurrentRevision.RevisionId,
            rfq.CurrentRevision.QuoteSeedRevisionId,
            currentUser.User.UserId,
            now,
            cancellationToken);
        events.Record(new RfqTransition(
            RfqTransitionKind.RevisionConfirmed,
            rfq.CaseId.Value,
            currentUser.User.UserId.Value,
            now,
            From: rfq.PreviousRevision?.RevisionId.Value.ToString(),
            To: rfq.CurrentRevision.RevisionId.Value.ToString()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SaveAmendment.ToResult(rfq);
    }
}

public sealed class DiscardAmendment(
    IRfqCaseRepository cases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<AmendmentResult> ExecuteAsync(
        AmendmentItem command,
        CancellationToken cancellationToken = default)
    {
        var rfq = await CloseRfq.LoadAsync(cases, command.CaseId, cancellationToken);
        authorization.EnsureCanDiscardRevision(currentUser.User, rfq);
        rfq.DiscardAmendment(command.ExpectedCurrentVersion, command.ExpectedDraftVersion);
        cases.Update(rfq);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SaveAmendment.ToResult(rfq);
    }
}

public sealed class BulkConfirmAmendments(ConfirmAmendment confirm)
{
    public Task<IReadOnlyList<AmendmentItemResult>> ExecuteAsync(
        IReadOnlyList<AmendmentItem> items,
        CancellationToken cancellationToken = default) =>
        AmendmentBulk.ExecuteAsync(items, confirm.ExecuteAsync, "Confirmed", cancellationToken);
}

public sealed class BulkDiscardAmendments(DiscardAmendment discard)
{
    public Task<IReadOnlyList<AmendmentItemResult>> ExecuteAsync(
        IReadOnlyList<AmendmentItem> items,
        CancellationToken cancellationToken = default) =>
        AmendmentBulk.ExecuteAsync(items, discard.ExecuteAsync, "Discarded", cancellationToken);
}

internal static class AmendmentBulk
{
    public static async Task<IReadOnlyList<AmendmentItemResult>> ExecuteAsync(
        IReadOnlyList<AmendmentItem> items,
        Func<AmendmentItem, CancellationToken, Task<AmendmentResult>> action,
        string success,
        CancellationToken cancellationToken)
    {
        var results = new List<AmendmentItemResult>(items.Count);
        foreach (var item in items)
        {
            try
            {
                await action(item, cancellationToken);
                results.Add(new(item.CaseId, success, null));
            }
            catch (Exception exception) when (exception is ArgumentException
                or InvalidOperationException or KeyNotFoundException
                or UnauthorizedAccessException)
            {
                results.Add(new(item.CaseId, "Failed", exception.Message));
            }
        }
        return results;
    }
}

public sealed class CreateFromExisting(
    IRfqCaseRepository cases,
    InitialRfqFactory factory,
    ISystemDateProvider systemDate,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<InitialRfqResult> ExecuteAsync(
        long sourceCaseId,
        CancellationToken cancellationToken = default)
    {
        authorization.EnsureCanCreateRevision(currentUser.User);
        var source = await CloseRfq.LoadAsync(cases, sourceCaseId, cancellationToken);
        var today = await systemDate.GetTodayAsync(cancellationToken);
        var settlement = DateOnly.FromDateTime(source.CreatedAt.UtcDateTime) == today
            ? source.CurrentRevision.SettlementDate
            : null;
        var copy = await factory.CreateAsync(new CreateDraftCommand(
            source.ClientId.Value,
            source.SecurityId.Value,
            source.CurrentRevision.Notional,
            settlement,
            source.CurrentRevision.SalesAndTradingMessage,
            null), cancellationToken, source.CaseId, source.CurrentRevision.RevisionId);
        cases.Add(copy);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return InitialRfqResult.From(copy);
    }
}
