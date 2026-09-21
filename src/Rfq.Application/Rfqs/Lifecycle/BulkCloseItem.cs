using Rfq.Domain;

namespace Rfq.Application;

public sealed record BulkCloseItem(
    long CaseId,
    long ExpectedCurrentVersion);
