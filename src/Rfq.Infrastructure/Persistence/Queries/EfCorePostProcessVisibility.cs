using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCorePostProcessVisibility(RfqDbContext dbContext)
    : IPostProcessVisibility
{
    public async Task<IReadOnlySet<CaseId>> GetPermittedCaseIdsAsync(
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        long[] caseIds = await dbContext.RfqCases
            .AsNoTracking()
            .Where(rfq => dbContext.MasterUsers.Any(user =>
                user.UserId == rfq.Current.AssignedTraderId
                && user.DeskId == currentUser.DeskId.Value))
            .Select(rfq => rfq.CaseId)
            .ToArrayAsync(cancellationToken);
        return caseIds.Select(value => new CaseId(value)).ToHashSet();
    }
}
