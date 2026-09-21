using Rfq.Domain;

namespace Rfq.Application;

public sealed class UpdateSalesMemo(
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
        authorization.EnsureCanUpdateSalesMemo(currentUser.User);
        var rfqCase = await CloseRfq.LoadAsync(rfqCases, caseId, cancellationToken);
        await EnsureDeskAccessAsync(users, currentUser.User, rfqCase, cancellationToken);
        var caseMemo = await GetMemoAsync(memos, caseId, cancellationToken);
        caseMemo = CaseMemoTransitions.UpdateSales(
            caseMemo, memo, new StateVersion(expectedVersion));
        memos.Update(caseMemo);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CaseMemoResult(caseId, caseMemo.SalesMemo, caseMemo.Version.Value);
    }

    internal static async Task<CaseMemo> GetMemoAsync(
        ICaseMemoRepository memos,
        long caseId,
        CancellationToken cancellationToken) =>
        await memos.GetAsync(new CaseId(caseId), cancellationToken)
            ?? throw new KeyNotFoundException($"Case Memo for RFQ Case '{caseId}' was not found.");

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
            throw new UnauthorizedAccessException(
                "The RFQ Case is outside the current user's desk scope.");
        }
    }
}
