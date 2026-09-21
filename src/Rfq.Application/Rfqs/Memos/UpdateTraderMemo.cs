using Rfq.Domain;

namespace Rfq.Application;

public sealed class UpdateTraderMemo(
    IRfqCaseRepository rfqCases,
    ICaseMemoRepository memos,
    IUserDirectory users,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<CaseMemoResult> ExecuteAsync(
        long caseId,
        string? memo,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        authorization.EnsureCanUpdateTraderMemo(currentUser.User);
        var rfqCase = await CloseRfq.LoadAsync(rfqCases, caseId, cancellationToken);
        await UpdateSalesMemo.EnsureDeskAccessAsync(
            users,
            currentUser.User,
            rfqCase,
            cancellationToken);
        var caseMemo = await UpdateSalesMemo.GetMemoAsync(
            memos,
            caseId,
            cancellationToken);
        caseMemo = CaseMemoTransitions.UpdateTrader(
            caseMemo, memo, new StateVersion(expectedVersion));
        memos.Update(caseMemo);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CaseMemoResult(caseId, caseMemo.TraderMemo, caseMemo.Version.Value);
    }
}
