using Rfq.Domain;

namespace Rfq.Application;

public sealed record LifecycleResult(
    long CaseId,
    string RfqStatus,
    string? QuoteStatus,
    string? QuoteRequestReason,
    long CurrentVersion);
