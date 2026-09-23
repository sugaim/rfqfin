using Rfq.Domain;

namespace Rfq.Application;

public sealed class GetActiveTraderRfqs(
    ITraderRfqQueries activeRfqs,
    IRfqAuthorization authorization,
    IBusinessDateProvider businessDateProvider,
    ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<TraderRfqListItem>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        authorization.EnsureCanViewTraderScreen(currentUser.User);
        DateOnly businessDate = await businessDateProvider.GetCurrentAsync(cancellationToken);
        try
        {
            return await activeRfqs.GetAsync(
                currentUser.User.DeskId,
                businessDate,
                cancellationToken);
        }
        catch (BusinessDateChangedException)
        {
            businessDate = await businessDateProvider.GetCurrentAsync(cancellationToken);
            return await activeRfqs.GetAsync(
                currentUser.User.DeskId,
                businessDate,
                cancellationToken);
        }
    }
}

public sealed record TraderRfqListItem(
    CaseId CaseId,
    ClientId ClientId,
    string ClientName,
    SecurityId SecurityId,
    string SecurityJapaneseName,
    string SecurityBbgDisplay,
    CategoryId CategoryId,
    RfqStatus RfqStatus,
    QuoteStatus? QuoteStatus,
    QuoteRequestReason? QuoteRequestReason,
    RevisionId CurrentRevisionId,
    QuoteId? CurrentQuoteId,
    QuoteId? ClosedQuoteId,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? ExpiresAt,
    RevisionId? QuoteSeedRevisionId,
    UserId ContactOwnerId,
    UserId AssignedTraderId,
    bool Owned,
    StateVersion CurrentVersion,
    DateOnly? SettlementDate,
    decimal? Notional,
    string SalesAndTradingMessage,
    WorkingQuoteMode WorkingQuoteMode,
    CalculatedQuotePayload? Calculated,
    ManualQuotePayload? Manual,
    StateVersion WorkingQuoteVersion,
    string TraderMemo,
    StateVersion TraderMemoVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset StateSince);
