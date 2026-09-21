using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCoreEodQueries(
    RfqDbContext dbContext,
    ICurrentUser currentUser) : IEodQueries
{
    public async Task<IReadOnlyList<EodSummaryItem>> GetEodAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var deskId = currentUser.User.DeskId.Value;
        var deskTimeZone = await ResolveDeskTimeZoneAsync(cancellationToken);
        var utcFrom = DeskDateBoundary.ToUtc(date, deskTimeZone);
        var utcTo = DeskDateBoundary.ToUtc(date.AddDays(1), deskTimeZone);
        var openRows = await dbContext.RfqCases.AsNoTracking()
            .Where(item => dbContext.MasterUsers.Any(user =>
                    user.UserId == item.Current.AssignedTraderId
                    && user.DeskId == deskId)
                && (item.Current.RfqStatus == RfqStatus.Active
                    || item.Current.RfqStatus == RfqStatus.Presented))
            .Select(item => item.Current.ContactOwnerId)
            .ToListAsync(cancellationToken);
        var closedHitType = RfqTransitionKind.ClosedHit.ToString();
        var closedAwayType = RfqTransitionKind.ClosedAway.ToString();
        var closeRows = await (
            from rfqEvent in dbContext.RfqEvents.AsNoTracking()
            join eventEntity in dbContext.Events.AsNoTracking()
                on rfqEvent.EventId equals eventEntity.EventId
            join rfq in dbContext.RfqCases.AsNoTracking()
                on rfqEvent.CaseId equals rfq.CaseId
            where eventEntity.OccurredAt >= utcFrom
                && eventEntity.OccurredAt < utcTo
                && (rfqEvent.Type == closedHitType || rfqEvent.Type == closedAwayType)
                && dbContext.MasterUsers.Any(user =>
                    user.UserId == rfq.Current.AssignedTraderId
                    && user.DeskId == deskId)
            select new
            {
                rfq.Current.ContactOwnerId,
                rfqEvent.Type,
            }).ToListAsync(cancellationToken);

        var ownerIds = openRows.Concat(closeRows.Select(item => item.ContactOwnerId))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        return ownerIds.Select(ownerId => new EodSummaryItem(
            UserId.Create(ownerId),
            openRows.Count(item => item == ownerId),
            closeRows.Count(item => item.ContactOwnerId == ownerId && item.Type == closedHitType),
            closeRows.Count(item => item.ContactOwnerId == ownerId && item.Type == closedAwayType)))
            .ToArray();
    }

    private async Task<TimeZoneInfo> ResolveDeskTimeZoneAsync(
        CancellationToken cancellationToken)
    {
        var deskId = currentUser.User.DeskId.Value;
        var timeZoneId = await dbContext.Desks.AsNoTracking()
            .Where(item => item.DeskId == deskId)
            .Select(item => item.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Desk '{deskId}' was not found.");
        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }
}
