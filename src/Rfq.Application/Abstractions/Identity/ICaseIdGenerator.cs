using Rfq.Domain;

namespace Rfq.Application;

public interface ICaseIdGenerator
{
    Task<CaseId> NextAsync(CancellationToken cancellationToken = default);
}
