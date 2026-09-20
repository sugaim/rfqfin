namespace Rfq.Application;

public sealed record SalesRfqListItem(
    Guid CaseId,
    string ClientId,
    string SecurityId,
    string RfqStatus,
    Guid CurrentRevisionId,
    string RevisionStatus,
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
