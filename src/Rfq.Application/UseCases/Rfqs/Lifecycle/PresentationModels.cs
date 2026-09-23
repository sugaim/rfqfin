using Rfq.Domain;

namespace Rfq.Application;

public sealed record PresentationResult(
    CaseId CaseId,
    QuoteId QuoteId,
    RfqStatus RfqStatus,
    QuoteStatus QuoteStatus,
    StateVersion CurrentVersion);
