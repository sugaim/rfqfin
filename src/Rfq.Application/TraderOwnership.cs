using Rfq.Domain;

namespace Rfq.Application;

public sealed record TraderRfqListItem(
    long CaseId,
    string ClientId,
    string ClientName,
    string SecurityId,
    string SecurityJapaneseName,
    string SecurityBbgDisplay,
    string CategoryId,
    string RfqStatus,
    string? QuoteStatus,
    string? QuoteRequestReason,
    string ContactOwnerId,
    string AssignedTraderId,
    bool Owned,
    long CurrentVersion,
    DateOnly? SettlementDate,
    decimal? Notional,
    DateTimeOffset CreatedAt);

public sealed record OwnershipResult(
    long CaseId,
    string AssignedTraderId,
    bool Owned,
    long CurrentVersion);

public sealed class GetActiveTraderRfqs(
    IRfqCaseRepository rfqCases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser)
{
    public Task<IReadOnlyList<TraderRfqListItem>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        authorization.EnsureCanViewTraderScreen(currentUser.User);
        return rfqCases.GetActiveTraderRfqsAsync(
            currentUser.User.DeskId,
            cancellationToken);
    }
}

public sealed class PickUpRfq(
    IRfqCaseRepository rfqCases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<OwnershipResult> ExecuteAsync(
        long caseId,
        long expectedVersion,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await OwnershipUseCase.LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanPickUp(currentUser.User, rfqCase, confirmed);
        rfqCase.PickUp(currentUser.User.UserId, expectedVersion);
        return await OwnershipUseCase.SaveAsync(rfqCases, unitOfWork, rfqCase, cancellationToken);
    }
}

public sealed class ReleaseRfq(
    IRfqCaseRepository rfqCases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<OwnershipResult> ExecuteAsync(
        long caseId,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await OwnershipUseCase.LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanRelease(currentUser.User, rfqCase);
        rfqCase.Release(currentUser.User.UserId, expectedVersion);
        return await OwnershipUseCase.SaveAsync(rfqCases, unitOfWork, rfqCase, cancellationToken);
    }
}

public sealed class AssignTrader(
    IRfqCaseRepository rfqCases,
    AssignedTraderValidator assignedTraderValidator,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<OwnershipResult> ExecuteAsync(
        long caseId,
        string targetTraderId,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await OwnershipUseCase.LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanAssignTrader(currentUser.User, rfqCase);
        var target = await assignedTraderValidator.ResolveAsync(
            targetTraderId,
            rfqCase.AssignedTraderId.Value,
            cancellationToken);
        rfqCase.AssignTo(target, expectedVersion);
        return await OwnershipUseCase.SaveAsync(rfqCases, unitOfWork, rfqCase, cancellationToken);
    }
}

public sealed class TakeOverRfq(
    IRfqCaseRepository rfqCases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<OwnershipResult> ExecuteAsync(
        long caseId,
        long expectedVersion,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await OwnershipUseCase.LoadAsync(rfqCases, caseId, cancellationToken);
        authorization.EnsureCanTakeOver(currentUser.User, rfqCase, confirmed);
        rfqCase.TakeOver(currentUser.User.UserId, expectedVersion);
        return await OwnershipUseCase.SaveAsync(rfqCases, unitOfWork, rfqCase, cancellationToken);
    }
}

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
            rfqCase.Owned,
            rfqCase.CurrentVersion);
    }
}
