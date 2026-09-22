using Rfq.Domain;

namespace Rfq.Application;

public sealed class UpdateMemoOperation(
    IRfqCaseRepository rfqCases,
    IRfqMemoRepository memos,
    IUserDirectory users,
    IRfqAuthorization authorization,
    ICurrentUser currentUser)
{
    public async Task<MemoResult> ApplySalesAsync(
        CaseId caseId,
        string? memo,
        StateVersion expectedVersion,
        CancellationToken cancellationToken)
    {
        authorization.EnsureCanUpdateSalesMemo(currentUser.User);
        _ = await LoadAndAuthorizeAsync(caseId, cancellationToken);
        SalesMemo salesMemo = await UpdateSalesMemo.GetMemoAsync(
            memos,
            caseId,
            cancellationToken);
        salesMemo = SalesMemoTransitions.Update(salesMemo, memo, expectedVersion);
        memos.Update(salesMemo);
        return new MemoResult(caseId, salesMemo.Value, salesMemo.Version);
    }

    public async Task<MemoResult> ApplyTraderAsync(
        CaseId caseId,
        string? memo,
        StateVersion expectedVersion,
        CancellationToken cancellationToken)
    {
        authorization.EnsureCanUpdateTraderMemo(currentUser.User);
        _ = await LoadAndAuthorizeAsync(caseId, cancellationToken);
        TraderMemo traderMemo = await memos.GetTraderAsync(caseId, cancellationToken)
            ?? throw new RfqInvariantException(
                $"Trader Memo for RFQ Case '{caseId}' was not found.");
        traderMemo = TraderMemoTransitions.Update(
            traderMemo,
            memo,
            expectedVersion);
        memos.Update(traderMemo);
        return new MemoResult(caseId, traderMemo.Value, traderMemo.Version);
    }

    public Task<MemoResult> ApplyOwnAsync(
        CaseId caseId,
        string? memo,
        StateVersion expectedVersion,
        CancellationToken cancellationToken)
    {
        if (currentUser.User.Roles.Contains(UserRole.Sales))
        {
            return ApplySalesAsync(
                caseId,
                memo,
                expectedVersion,
                cancellationToken);
        }

        if (currentUser.User.Roles.Contains(UserRole.Trader))
        {
            return ApplyTraderAsync(
                caseId,
                memo,
                expectedVersion,
                cancellationToken);
        }

        throw new RfqForbiddenException(
            "The Sales or Trader role is required to update My Memo.");
    }

    private async Task<RfqCase> LoadAndAuthorizeAsync(
        CaseId caseId,
        CancellationToken cancellationToken)
    {
        RfqCase rfqCase = await ClosedRfqUseCase.LoadAsync(
            rfqCases,
            caseId,
            cancellationToken);
        await UpdateSalesMemo.EnsureDeskAccessAsync(
            users,
            currentUser.User,
            rfqCase,
            cancellationToken);
        return rfqCase;
    }
}
