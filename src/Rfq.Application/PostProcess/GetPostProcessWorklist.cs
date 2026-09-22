using Rfq.Domain;

namespace Rfq.Application;

public sealed class GetPostProcessWorklist(
    IPostProcessQueries queries,
    IPostProcessVisibility visibility,
    IBusinessDateProvider businessDateProvider,
    ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<PostProcessWorklistItem>> ExecuteAsync(
        PostProcessPreset preset,
        PostProcessScope scope,
        CancellationToken cancellationToken = default)
    {
        DateOnly businessDate = await businessDateProvider.GetCurrentAsync(
            cancellationToken);
        IReadOnlySet<CaseId> permitted =
            await visibility.GetPermittedCaseIdsAsync(
                currentUser.User,
                cancellationToken);
        return await queries.GetAsync(
            preset,
            scope,
            businessDate,
            currentUser.User,
            permitted,
            cancellationToken);
    }
}
