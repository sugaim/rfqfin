using Rfq.Domain;

namespace Rfq.Application;

public sealed record OwnershipResult(
    long CaseId,
    string AssignedTraderId,
    bool Owned,
    long CurrentVersion);
