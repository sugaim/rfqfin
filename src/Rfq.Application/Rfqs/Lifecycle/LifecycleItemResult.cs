using Rfq.Domain;

namespace Rfq.Application;

public sealed record LifecycleItemResult(long CaseId, string Result, string? Error);
