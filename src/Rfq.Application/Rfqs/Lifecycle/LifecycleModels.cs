using Rfq.Domain;

namespace Rfq.Application;

public sealed record LifecycleResult(
    CaseId CaseId,
    RfqStatus RfqStatus,
    QuoteStatus? QuoteStatus,
    QuoteRequestReason? QuoteRequestReason,
    StateVersion CurrentVersion);

public sealed record LifecycleItem(CaseId CaseId, StateVersion ExpectedCurrentVersion);

public sealed record LifecycleItemResult(CaseId CaseId, string Result, string? Error);

public sealed record BulkCloseItem(
    CaseId CaseId,
    StateVersion ExpectedCurrentVersion);

public sealed record BulkCloseItemResult(
    CaseId CaseId,
    string Result,
    RfqStatus? RfqStatus,
    string? Error);

public sealed record CloseRfqResult(
    CaseId CaseId,
    RfqStatus RfqStatus,
    QuoteId ClosedQuoteId,
    bool Owned,
    StateVersion CurrentVersion);
