using Rfq.Domain;

namespace Rfq.Application;

public interface IRfqCaseRepository
{
    void Add(RfqCase rfqCase);

    Task<RfqCase?> GetAsync(CaseId caseId, CancellationToken cancellationToken = default);

    void Update(RfqCase rfqCase);

    void UpdateRevision(RfqRevision revision);

    Task<IReadOnlyList<SalesRfqListItem>> GetActiveSalesRfqsAsync(
        UserId salesUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TraderRfqListItem>> GetActiveTraderRfqsAsync(
        DeskId deskId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpiredQuoteCandidate>> GetExpiredQuotesAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
