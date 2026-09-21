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
}

public interface IWorkingQuoteEnsurer
{
    Task EnsureAsync(
        RevisionId revisionId,
        UserId createdBy,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default);
}
