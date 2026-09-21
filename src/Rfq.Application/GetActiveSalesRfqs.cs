namespace Rfq.Application;

public sealed record SalesRfqListItem(
    long CaseId,
    string ClientId,
    string ClientName,
    string SecurityId,
    string SecurityJapaneseName,
    string SecurityBbgDisplay,
    string CategoryId,
    string RfqStatus,
    Guid CurrentRevisionId,
    string RevisionStatus,
    string ContactOwnerId,
    string AssignedTraderId,
    DateOnly SettlementDate,
    DateOnly StandardSettlementDate,
    DateTimeOffset CreatedAt);

public sealed class GetActiveSalesRfqs(
    IRfqCaseRepository rfqCases,
    ICurrentUser currentUser)
{
    public Task<IReadOnlyList<SalesRfqListItem>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        return rfqCases.GetActiveSalesRfqsAsync(
            currentUser.User.UserId,
            cancellationToken);
    }
}
