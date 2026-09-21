using Rfq.Domain;

namespace Rfq.Application;

public interface IConfirmedQuoteRepository
{
    void Add(ConfirmedQuote quote);

    Task<ConfirmedQuote?> GetAsync(
        QuoteId quoteId,
        CancellationToken cancellationToken = default);
}

public enum QuoteTransitionKind
{
    Confirmed,
    Presented,
    Unpresented,
}

public sealed record QuoteTransition(
    QuoteTransitionKind Kind,
    long CaseId,
    Guid QuoteId,
    string PerformedBy,
    DateTimeOffset OccurredAt);

public interface IQuoteEventSink
{
    void Record(QuoteTransition transition);
}

public sealed record ConfirmQuoteResult(
    long CaseId,
    Guid QuoteId,
    Guid RevisionId,
    string RfqStatus,
    string QuoteStatus,
    string Mode,
    CalculatedQuotePayload? Calculated,
    ManualQuotePayload? Manual,
    DateTimeOffset ConfirmedAt,
    int? ExpiryMinutes,
    DateTimeOffset? ExpiresAt,
    long CurrentVersion);

public sealed record PresentationResult(
    long CaseId,
    Guid QuoteId,
    string RfqStatus,
    string QuoteStatus,
    long CurrentVersion);

public sealed class ConfirmQuote(
    IRfqCaseRepository rfqCases,
    IWorkingQuoteRepository workingQuotes,
    IConfirmedQuoteRepository confirmedQuotes,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IQuoteEventSink eventSink,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<ConfirmQuoteResult> ExecuteAsync(
        long caseId,
        int? expiryMinutes,
        long expectedCurrentVersion,
        long expectedWorkingQuoteVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await UpdateInitialDraft.GetCaseAsync(
            rfqCases,
            caseId,
            cancellationToken);
        authorization.EnsureCanConfirmQuote(currentUser.User, rfqCase);
        if (rfqCase.CurrentVersion != expectedCurrentVersion)
        {
            throw new InvalidOperationException("The RFQ Case was changed by another user.");
        }

        var open = (OpenRfq)rfqCase.Lifecycle;
        var workingQuote = await workingQuotes.GetAsync(
            open.CurrentRevisionId,
            cancellationToken)
            ?? throw new KeyNotFoundException("WorkingQuote was not found.");
        if (workingQuote.Version != expectedWorkingQuoteVersion)
        {
            throw new InvalidOperationException("The WorkingQuote was changed by another user.");
        }

        var requestReason = open.QuoteRequestReason
            ?? throw new InvalidOperationException("Requested RFQ is missing QuoteRequestReason.");
        var settlementDate = rfqCase.InitialRevision.SettlementDate
            ?? throw new InvalidOperationException("Current Revision is missing SettlementDate.");
        var now = timeProvider.GetUtcNow();
        var confirmedQuote = ConfirmedQuote.Create(
            QuoteId.New(),
            workingQuote,
            rfqCase.SecurityId,
            settlementDate,
            currentUser.User.UserId,
            now,
            expiryMinutes,
            requestReason);

        rfqCase.ConfirmQuote(
            confirmedQuote.QuoteId,
            workingQuote.RevisionId,
            expectedCurrentVersion);
        confirmedQuotes.Add(confirmedQuote);
        rfqCases.Update(rfqCase);
        eventSink.Record(new QuoteTransition(
            QuoteTransitionKind.Confirmed,
            caseId,
            confirmedQuote.QuoteId.Value,
            currentUser.User.UserId.Value,
            now));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ConfirmQuoteResult(
            caseId,
            confirmedQuote.QuoteId.Value,
            confirmedQuote.RevisionId.Value,
            rfqCase.Status.ToString(),
            rfqCase.QuoteStatus!.Value.ToString(),
            confirmedQuote.Mode.ToString(),
            confirmedQuote.Calculated,
            confirmedQuote.Manual,
            confirmedQuote.ConfirmedAt,
            confirmedQuote.ExpiryMinutes,
            confirmedQuote.ExpiresAt,
            rfqCase.CurrentVersion);
    }
}

public sealed class PresentQuote(
    IRfqCaseRepository rfqCases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IQuoteEventSink eventSink,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<PresentationResult> ExecuteAsync(
        long caseId,
        long expectedCurrentVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await UpdateInitialDraft.GetCaseAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanPresent(currentUser.User, rfqCase);
        rfqCase.Present(expectedCurrentVersion);

        var quoteId = rfqCase.CurrentQuoteId
            ?? throw new InvalidOperationException("Current ConfirmedQuote was not found.");
        rfqCases.Update(rfqCase);
        eventSink.Record(new QuoteTransition(
            QuoteTransitionKind.Presented,
            caseId,
            quoteId.Value,
            currentUser.User.UserId.Value,
            timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new PresentationResult(
            caseId,
            quoteId.Value,
            rfqCase.Status.ToString(),
            rfqCase.QuoteStatus!.Value.ToString(),
            rfqCase.CurrentVersion);
    }
}

public sealed class UnpresentQuote(
    IRfqCaseRepository rfqCases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IQuoteEventSink eventSink,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<PresentationResult> ExecuteAsync(
        long caseId,
        long expectedCurrentVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await UpdateInitialDraft.GetCaseAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanPresent(currentUser.User, rfqCase);
        rfqCase.Unpresent(expectedCurrentVersion);
        var quoteId = rfqCase.CurrentQuoteId
            ?? throw new InvalidOperationException("Current ConfirmedQuote was not found.");
        rfqCases.Update(rfqCase);
        eventSink.Record(new QuoteTransition(
            QuoteTransitionKind.Unpresented,
            caseId,
            quoteId.Value,
            currentUser.User.UserId.Value,
            timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new PresentationResult(
            caseId,
            quoteId.Value,
            rfqCase.Status.ToString(),
            rfqCase.QuoteStatus!.Value.ToString(),
            rfqCase.CurrentVersion);
    }
}
