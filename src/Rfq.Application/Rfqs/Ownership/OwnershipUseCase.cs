using Rfq.Domain;

namespace Rfq.Application;

internal static class OwnershipUseCase
{
    public static async Task<RfqCase> LoadAsync(
        IRfqCaseRepository rfqCases,
        long caseId,
        CancellationToken cancellationToken) =>
        await rfqCases.GetAsync(new CaseId(caseId), cancellationToken)
            ?? throw new KeyNotFoundException($"RFQ Case '{caseId}' was not found.");

    public static async Task<OwnershipResult> SaveAsync(
        IRfqCaseRepository rfqCases,
        IUnitOfWork unitOfWork,
        RfqCase rfqCase,
        CancellationToken cancellationToken)
    {
        rfqCases.Update(rfqCase);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new OwnershipResult(
            rfqCase.CaseId.Value,
            rfqCase.AssignedTraderId.Value,
            rfqCase.Ownership is Owned,
            rfqCase.Version.Value);
    }
}
