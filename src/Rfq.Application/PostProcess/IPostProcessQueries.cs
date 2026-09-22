using Rfq.Domain;

namespace Rfq.Application;

public interface IPostProcessQueries
{
    Task<IReadOnlyList<PostProcessWorklistItem>> GetAsync(
        PostProcessPreset preset,
        PostProcessScope scope,
        DateOnly currentBusinessDate,
        CurrentUser currentUser,
        IReadOnlySet<CaseId> permittedCaseIds,
        CancellationToken cancellationToken = default);
}
