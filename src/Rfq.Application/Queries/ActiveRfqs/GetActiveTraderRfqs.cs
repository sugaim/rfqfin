using Rfq.Domain;

namespace Rfq.Application;

public sealed class GetActiveTraderRfqs(
    IActiveRfqQueries activeRfqs,
    IRfqAuthorization authorization,
    ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<TraderRfqListItem>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        authorization.EnsureCanViewTraderScreen(currentUser.User);
        return await activeRfqs.GetTraderAsync(
            currentUser.User.DeskId,
            cancellationToken);
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
    WorkingQuoteMode WorkingQuoteMode,
    CalculatedQuotePayload? Calculated,
    ManualQuotePayload? Manual,
    StateVersion WorkingQuoteVersion,
    string TraderMemo,
    StateVersion MemoVersion,
    DateTimeOffset CreatedAt);
