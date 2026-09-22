using Rfq.Domain;

namespace Rfq.Application;

internal static class OwnershipUseCase
{
    public static async Task<RfqCase> LoadAsync(
        IRfqCaseRepository rfqCases,
        CaseId caseId,
        CancellationToken cancellationToken) =>
        await rfqCases.GetAsync(caseId, cancellationToken)
            ?? throw new RfqNotFoundException($"RFQ Case '{caseId}' was not found.");

    public static async Task<OwnershipResult> SaveAsync(
        IRfqCaseRepository rfqCases,
        IUnitOfWork unitOfWork,
        RfqCase rfqCase,
        CancellationToken cancellationToken)
    {
        rfqCases.Update(rfqCase);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResult(rfqCase);
    }

    public static OwnershipResult ToResult(RfqCase rfqCase) => new(
        rfqCase.CaseId,
        rfqCase.AssignedTraderId,
        rfqCase.Ownership is Owned,
        rfqCase.Version);
}

public sealed record OwnershipResult(
    CaseId CaseId,
    UserId AssignedTraderId,
    bool Owned,
    StateVersion CurrentVersion);
