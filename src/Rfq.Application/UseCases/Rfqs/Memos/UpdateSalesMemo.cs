using Rfq.Domain;

namespace Rfq.Application;

public sealed class UpdateSalesMemo(
    UpdateMemoOperation operation,
    IUnitOfWork unitOfWork)
{
    public async Task<MemoResult> ExecuteAsync(
        CaseId caseId,
        string? memo,
        StateVersion expectedVersion,
        CancellationToken cancellationToken = default)
    {
        MemoResult result = await operation.ApplySalesAsync(
            caseId,
            memo,
            expectedVersion,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }

    internal static async Task<SalesMemo> GetMemoAsync(
        IRfqMemoRepository memos,
        CaseId caseId,
        CancellationToken cancellationToken) =>
        await memos.GetSalesAsync(caseId, cancellationToken)
            ?? throw new RfqInvariantException(
                $"Sales Memo for RFQ Case '{caseId}' was not found.");

    internal static async Task EnsureDeskAccessAsync(
        IUserDirectory users,
        CurrentUser user,
        RfqCase rfqCase,
        CancellationToken cancellationToken)
    {
        UserSummary? assignedTrader = await users.ResolveAsync(
            rfqCase.AssignedTraderId,
            cancellationToken);
        if (assignedTrader is null || assignedTrader.DeskId != user.DeskId)
        {
            throw new RfqForbiddenException(
                "The RFQ Case is outside the current user's desk scope.");
        }
    }
}
