using Rfq.Domain;

namespace Rfq.Application;

public sealed record ContactOwnerResult(
    long CaseId,
    string ContactOwnerId,
    long CurrentVersion);
