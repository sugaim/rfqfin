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
    Withdrawn,
    Expired,
}

public sealed record QuoteTransition(
    QuoteTransitionKind Kind,
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
        if (rfqCase.Version != new StateVersion(expectedCurrentVersion))
        {
            throw new StateVersionMismatchException("The RFQ Case was changed by another user.");
        }

        var workingQuote = await workingQuotes.GetAsync(
            rfqCase.CurrentRevision.RevisionId,
            cancellationToken)
            ?? throw new KeyNotFoundException("WorkingQuote was not found.");
        if (workingQuote.Version != new StateVersion(expectedWorkingQuoteVersion))
        {
            throw new StateVersionMismatchException("The WorkingQuote was changed by another user.");
        }

        var now = timeProvider.GetUtcNow();
        var transition = QuoteTransitions.Confirm(
            rfqCase, workingQuote, QuoteId.New(),
            new QuoteConfirmation(currentUser.User.UserId, now,
                QuoteExpiry.FromMinutes(expiryMinutes)));
        rfqCase = transition.Rfq;
        var confirmedQuote = transition.ConfirmedQuote;
        confirmedQuotes.Add(confirmedQuote);
        rfqCases.Update(rfqCase);
        eventSink.Record(new QuoteTransition(
            QuoteTransitionKind.Confirmed,
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
            rfqCase.Version.Value);
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
        rfqCase = RfqLifecycleTransitions.Present(
            rfqCase, new StateVersion(expectedCurrentVersion));

        var quoteId = rfqCase.CurrentQuoteId
            ?? throw new InvalidOperationException("Current ConfirmedQuote was not found.");
        rfqCases.Update(rfqCase);
        eventSink.Record(new QuoteTransition(
            QuoteTransitionKind.Presented,
            quoteId.Value,
            currentUser.User.UserId.Value,
            timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new PresentationResult(
            caseId,
            quoteId.Value,
            rfqCase.Status.ToString(),
            rfqCase.QuoteStatus!.Value.ToString(),
            rfqCase.Version.Value);
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
        rfqCase = RfqLifecycleTransitions.Unpresent(
            rfqCase, new StateVersion(expectedCurrentVersion));
        var quoteId = rfqCase.CurrentQuoteId
            ?? throw new InvalidOperationException("Current ConfirmedQuote was not found.");
        rfqCases.Update(rfqCase);
        eventSink.Record(new QuoteTransition(
            QuoteTransitionKind.Unpresented,
            quoteId.Value,
            currentUser.User.UserId.Value,
            timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new PresentationResult(
            caseId,
            quoteId.Value,
            rfqCase.Status.ToString(),
            rfqCase.QuoteStatus!.Value.ToString(),
            rfqCase.Version.Value);
    }
}
