using Rfq.Domain;

namespace Rfq.Application;

public interface IRfqCaseRepository
{
    void Add(RfqCase rfqCase);

    Task<RfqCase?> GetAsync(CaseId caseId, CancellationToken cancellationToken = default);

    void Update(RfqCase rfqCase);

    Task<IReadOnlyList<SalesRfqListItem>> GetActiveSalesRfqsAsync(
        UserId salesUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TraderRfqListItem>> GetActiveTraderRfqsAsync(
        string deskId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpiredQuoteCandidate>> GetExpiredQuotesAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ExpiredQuoteCandidate>>([]);
}

public sealed record ExpiredQuoteCandidate(long CaseId, Guid QuoteId, long CurrentVersion);

public interface IWorkingQuoteEnsurer
{
    Task<WorkingQuote> EnsureAsync(
        RevisionId revisionId,
        RevisionId? quoteSeedRevisionId,
        UserId createdBy,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default);
}
