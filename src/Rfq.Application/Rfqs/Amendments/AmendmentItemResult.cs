using Rfq.Domain;

namespace Rfq.Application;

public sealed record AmendmentItemResult(long CaseId, string Result, string? Error);
