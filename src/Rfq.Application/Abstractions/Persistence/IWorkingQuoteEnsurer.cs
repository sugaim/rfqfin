using Rfq.Domain;

namespace Rfq.Application;

public interface IWorkingQuoteEnsurer
{
    Task<WorkingQuote> EnsureAsync(
        RevisionId revisionId,
        RevisionId? quoteSeedRevisionId,
        UserId createdBy,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default);
}
