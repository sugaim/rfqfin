using Rfq.Domain;

namespace Rfq.Application;

public sealed record RfqSearch(
    DateOnly? CreatedFrom = null,
    DateOnly? CreatedTo = null,
    ClientId? ClientId = null,
    SecurityId? SecurityId = null,
    CategoryId? CategoryId = null,
    UserId? ContactOwnerId = null,
    UserId? SalesId = null,
    UserId? AssignedTraderId = null,
    RfqStatus? Status = null,
    CaseId? CaseId = null);

public sealed record RfqSearchResult(IReadOnlyList<RfqSearchItem> Items, bool RequiresNarrowing);

public sealed record RfqSearchItem(
    CaseId CaseId,
    DateTimeOffset CreatedAt,
    ClientId ClientId,
    string ClientName,
    SecurityId SecurityId,
    string SecurityName,
    CategoryId CategoryId,
    RfqStatus Status,
    QuoteStatus? QuoteStatus,
    UserId ContactOwnerId,
    UserId? SalesId,
    UserId AssignedTraderId,
    decimal? Notional,
    DateOnly? SettlementDate,
    decimal? Price = null,
    decimal? FinalSimpleYield = null,
    decimal? Ysc = null);
