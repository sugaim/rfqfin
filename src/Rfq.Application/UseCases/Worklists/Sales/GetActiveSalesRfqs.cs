using Rfq.Domain;

namespace Rfq.Application;

public sealed class GetActiveSalesRfqs(
    ISalesRfqQueries activeRfqs,
    IBusinessDateProvider businessDateProvider,
    ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<SalesRfqListItem>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        DateOnly businessDate = await businessDateProvider.GetCurrentAsync(cancellationToken);
        try
        {
            return await activeRfqs.GetAsync(
                currentUser.User.UserId,
                businessDate,
                cancellationToken);
        }
        catch (BusinessDateChangedException)
        {
            businessDate = await businessDateProvider.GetCurrentAsync(cancellationToken);
            return await activeRfqs.GetAsync(
                currentUser.User.UserId,
                businessDate,
                cancellationToken);
        }
    }
}

public sealed record SalesRfqListItem(
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
    StateVersion CurrentVersion,
    RevisionStatus RevisionStatus,
    UserId? SalesId,
    UserId ContactOwnerId,
    UserId AssignedTraderId,
    DateOnly? SettlementDate,
    DateOnly StandardSettlementDate,
    decimal? Notional,
    string SalesAndTradingMessage,
    string SalesMemo,
    StateVersion SalesMemoVersion,
    StateVersion Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset StateSince,
    SalesConfirmedQuoteSummary? ConfirmedQuote,
    RevisionId? DraftRevisionId,
    StateVersion? DraftVersion,
    DateOnly? DraftSettlementDate,
    decimal? DraftNotional,
    string? DraftSalesAndTradingMessage);

public sealed record SalesConfirmedQuoteSummary(
    QuoteId QuoteId,
    WorkingQuoteMode Mode,
    decimal? Price,
    decimal? BbgYield,
    decimal? FinalSimpleYield,
    decimal? GSpread,
    DateTimeOffset ConfirmedAt);
