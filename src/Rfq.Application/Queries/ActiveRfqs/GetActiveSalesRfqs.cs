using Rfq.Domain;

namespace Rfq.Application;

public sealed class GetActiveSalesRfqs(
    IActiveRfqQueries activeRfqs,
    ICurrentUser currentUser)
{
    public Task<IReadOnlyList<SalesRfqListItem>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        return activeRfqs.GetSalesAsync(
            currentUser.User.UserId,
            cancellationToken);
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
    UserId ContactOwnerId,
    UserId AssignedTraderId,
    DateOnly? SettlementDate,
    DateOnly StandardSettlementDate,
    decimal? Notional,
    string SalesAndTradingMessage,
    string SalesMemo,
    StateVersion MemoVersion,
    StateVersion Version,
    DateTimeOffset CreatedAt,
    RevisionId? DraftRevisionId,
    StateVersion? DraftVersion,
    DateOnly? DraftSettlementDate,
    decimal? DraftNotional,
    string? DraftSalesAndTradingMessage);
