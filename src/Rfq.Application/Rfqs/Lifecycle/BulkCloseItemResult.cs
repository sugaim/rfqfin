using Rfq.Domain;

namespace Rfq.Application;

public sealed record BulkCloseItemResult(
    long CaseId,
    string Result,
    string? RfqStatus,
    string? Error);
