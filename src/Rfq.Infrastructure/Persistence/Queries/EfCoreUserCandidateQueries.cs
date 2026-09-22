using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCoreUserCandidateQueries(RfqDbContext dbContext) : IUserCandidateQueries
{
    public Task<IReadOnlyList<UserCandidate>> GetAssignableTradersAsync(
        DeskId deskId, CancellationToken cancellationToken = default) =>
        GetAsync(deskId, [UserRole.Trader.ToString()], cancellationToken);

    public Task<IReadOnlyList<UserCandidate>> GetContactOwnersAsync(
        DeskId deskId, CancellationToken cancellationToken = default) =>
        GetAsync(deskId, [UserRole.Sales.ToString(), UserRole.Trader.ToString()], cancellationToken);

    private async Task<IReadOnlyList<UserCandidate>> GetAsync(
        DeskId deskId,
        string[] roles,
        CancellationToken cancellationToken) => await dbContext.MasterUsers
        .AsNoTracking()
        .Where(user => user.DeskId == deskId.Value
            && user.Roles.Any(role => roles.Contains(role)))
        .OrderBy(user => user.Name)
        .Select(user => new UserCandidate(UserId.Create(user.UserId), user.Name))
        .ToListAsync(cancellationToken);
}
