using Rfq.Domain;

namespace Rfq.Application;

public sealed record PastRfqSearch(
    DateOnly? From = null, DateOnly? To = null, ClientId? ClientId = null,
    SecurityId? SecurityId = null, CategoryId? CategoryId = null, UserId? ContactOwnerId = null,
    UserId? SalesId = null, UserId? AssignedTraderId = null, RfqStatus? Status = null,
    CaseId? CaseId = null);

public sealed record PastRfqResult(IReadOnlyList<PastRfqItem> Items, bool RequiresNarrowing);

public sealed record PastRfqItem(
    CaseId CaseId, DateTimeOffset CreatedAt, ClientId ClientId, string ClientName,
    SecurityId SecurityId, string SecurityName, CategoryId CategoryId, RfqStatus Status,
    QuoteStatus? QuoteStatus, UserId ContactOwnerId, UserId? SalesId, UserId AssignedTraderId,
    decimal? Notional, DateOnly? SettlementDate);
