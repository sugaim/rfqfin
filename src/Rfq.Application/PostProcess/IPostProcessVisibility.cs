using Rfq.Domain;

namespace Rfq.Application;

public interface IPostProcessVisibility
{
    Task<IReadOnlySet<CaseId>> GetPermittedCaseIdsAsync(
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);
}
