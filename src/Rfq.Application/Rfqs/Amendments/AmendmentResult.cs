using Rfq.Domain;

namespace Rfq.Application;

public sealed record AmendmentResult(
    long CaseId,
    Guid CurrentRevisionId,
    Guid? DraftRevisionId,
    long CurrentVersion,
    long? DraftVersion,
    string RfqStatus,
    string? QuoteStatus,
    string? QuoteRequestReason);
