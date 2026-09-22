using Rfq.Domain;

namespace Rfq.Application;

public interface ISalesRecentRevisionQueries
{
    Task<IReadOnlyList<SalesRecentRevisionItem>> GetAsync(
        UserId salesUserId,
        int limit,
        CancellationToken cancellationToken = default);
}

public enum SalesRecentRevisionKind
{
    Rfq,
    Quote
}

public enum SalesRecentRevisionField
{
    Notional,
    Settlement,
    Message,
    Price,
    Yield,
    Simple,
    GSpread,
}

public sealed record SalesRecentRevisionChange(
    SalesRecentRevisionField Field,
    string? Before,
    string? After);

public sealed record SalesRecentRevisionItem(
    SalesRecentRevisionKind Kind,
    DateTimeOffset OccurredAt,
    CaseId CaseId,
    ClientId ClientId,
    string ClientName,
    SecurityId SecurityId,
    string SecurityName,
    IReadOnlyList<SalesRecentRevisionChange> Changes);
