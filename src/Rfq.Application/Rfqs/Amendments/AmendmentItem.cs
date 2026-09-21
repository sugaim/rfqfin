using Rfq.Domain;

namespace Rfq.Application;

public sealed record AmendmentItem(long CaseId, long ExpectedCurrentVersion, long ExpectedDraftVersion);
