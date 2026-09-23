using System.Collections.Immutable;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed record BusinessDateRfqSnapshot(
    DateOnly BusinessDate,
    long Generation,
    ImmutableArray<SalesRfqListItem> Sales,
    ImmutableArray<TraderRfqListItem> Trader,
    ImmutableDictionary<CaseId, RfqSnapshotRoute> Routes);

public sealed record RfqSnapshotRoute(
    CaseId CaseId,
    UserId? SalesId,
    UserId ContactOwnerId,
    UserId AssignedTraderId,
    DeskId? AssignedTraderDeskId,
    RevisionId CurrentRevisionId,
    QuoteId? CurrentQuoteId);

public sealed record RfqReadModelStatus(
    bool Available,
    DateOnly? RuntimeBusinessDate,
    DateOnly? SnapshotBusinessDate,
    long CurrentGeneration,
    long PublishedGeneration,
    DateTimeOffset? LastSuccessfulRefreshAt,
    string? LastFailure);
