using Rfq.Domain;

namespace Rfq.Application;

public sealed record PresentationResult(
    long CaseId,
    Guid QuoteId,
    string RfqStatus,
    string QuoteStatus,
    long CurrentVersion);
