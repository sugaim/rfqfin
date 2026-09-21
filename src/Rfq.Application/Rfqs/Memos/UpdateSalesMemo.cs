using Rfq.Domain;

namespace Rfq.Application;

public sealed class UpdateSalesMemo(
    IRfqCaseRepository rfqCases,
    IRfqMemoRepository memos,
    IUserDirectory users,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<MemoResult> ExecuteAsync(
        CaseId caseId,
        string? memo,
        StateVersion expectedVersion,
        CancellationToken cancellationToken = default)
    {
        authorization.EnsureCanUpdateSalesMemo(currentUser.User);
        var rfqCase = await ClosedRfqUseCase.LoadAsync(rfqCases, caseId, cancellationToken);
        await EnsureDeskAccessAsync(users, currentUser.User, rfqCase, cancellationToken);
        var salesMemo = await GetMemoAsync(memos, caseId, cancellationToken);
        salesMemo = SalesMemoTransitions.Update(salesMemo, memo, expectedVersion);
        memos.Update(salesMemo);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new MemoResult(caseId, salesMemo.Value, salesMemo.Version);
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
        var assignedTrader = await users.ResolveAsync(
            rfqCase.AssignedTraderId,
            cancellationToken);
        if (assignedTrader is null || assignedTrader.DeskId != user.DeskId)
        {
            throw new RfqForbiddenException(
                "The RFQ Case is outside the current user's desk scope.");
        }
    }
}
