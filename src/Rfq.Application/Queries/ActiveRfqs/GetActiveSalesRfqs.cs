namespace Rfq.Application;

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
