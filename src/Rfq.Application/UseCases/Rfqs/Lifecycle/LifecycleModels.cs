using Rfq.Domain;

namespace Rfq.Application;

public sealed record LifecycleResult(
    CaseId CaseId,
    RfqStatus RfqStatus,
    QuoteStatus? QuoteStatus,
    QuoteRequestReason? QuoteRequestReason,
    StateVersion CurrentVersion);

public sealed record LifecycleItem(CaseId CaseId, StateVersion ExpectedCurrentVersion);

public sealed record CloseRfqResult(
    CaseId CaseId,
    RfqStatus RfqStatus,
    QuoteId ClosedQuoteId,
    bool Owned,
    StateVersion CurrentVersion);
