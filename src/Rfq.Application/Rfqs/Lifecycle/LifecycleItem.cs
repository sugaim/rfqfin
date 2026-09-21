using Rfq.Domain;

namespace Rfq.Application;

public sealed record LifecycleItem(long CaseId, long ExpectedCurrentVersion);
