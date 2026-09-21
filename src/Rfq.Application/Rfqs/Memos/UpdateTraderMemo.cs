using Rfq.Domain;

namespace Rfq.Application;

public sealed class UpdateTraderMemo(
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
        authorization.EnsureCanUpdateTraderMemo(currentUser.User);
        var rfqCase = await CloseRfq.LoadAsync(rfqCases, caseId, cancellationToken);
        await UpdateSalesMemo.EnsureDeskAccessAsync(
            users,
            currentUser.User,
            rfqCase,
            cancellationToken);
        var traderMemo = await memos.GetTraderAsync(caseId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Trader Memo for RFQ Case '{caseId}' was not found.");
        traderMemo = TraderMemoTransitions.Update(traderMemo, memo, expectedVersion);
        memos.Update(traderMemo);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new MemoResult(caseId, traderMemo.Value, traderMemo.Version);
    }
}
