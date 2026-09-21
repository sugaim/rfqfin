using Rfq.Domain;

namespace Rfq.Application;

public sealed class GetActiveTraderRfqs(
    IRfqCaseRepository rfqCases,
    IWorkingQuoteEnsurer workingQuoteEnsurer,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<TraderRfqListItem>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        authorization.EnsureCanViewTraderScreen(currentUser.User);
        var items = await rfqCases.GetActiveTraderRfqsAsync(
            currentUser.User.DeskId,
            cancellationToken);
        var missing = items.Where(item => item.WorkingQuoteVersion == 0).ToArray();
        if (missing.Length == 0)
        {
            return items;
        }

        foreach (var item in missing)
        {
            await workingQuoteEnsurer.EnsureAsync(
                new RevisionId(item.CurrentRevisionId),
                item.QuoteSeedRevisionId is null
                    ? null
                    : new RevisionId(item.QuoteSeedRevisionId.Value),
                currentUser.User.UserId,
                timeProvider.GetUtcNow(),
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await rfqCases.GetActiveTraderRfqsAsync(
            currentUser.User.DeskId,
            cancellationToken);
    }
}
