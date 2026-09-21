using Rfq.Domain;

namespace Rfq.Application;

public sealed record CloseRfqResult(
    long CaseId,
    string RfqStatus,
    Guid ClosedQuoteId,
    bool Owned,
    long CurrentVersion);
