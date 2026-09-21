using Rfq.Domain;

namespace Rfq.Application;

public sealed class CreateFromExisting(
    IRfqCaseRepository cases,
    InitialRfqFactory factory,
    ISystemDateProvider systemDate,
    IBusinessDateResolver businessDateResolver,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<InitialRfqResult> ExecuteAsync(
        CaseId sourceCaseId,
        CancellationToken cancellationToken = default)
    {
        authorization.EnsureCanCreateRevision(currentUser.User);
        var source = await CloseRfq.LoadAsync(cases, sourceCaseId, cancellationToken);
        var today = await systemDate.GetTodayAsync(cancellationToken);
        var sourceBusinessDate = await businessDateResolver.ResolveAsync(
            source.CreatedAt, currentUser.User.DeskId, cancellationToken);
        var settlement = sourceBusinessDate == today
            ? source.CurrentRevision.SettlementDate
            : null;
        var copy = await factory.CreateAsync(new CreateDraftCommand(
            source.ClientId,
            source.SecurityId,
            source.CurrentRevision.Notional,
            settlement,
            source.CurrentRevision.SalesAndTradingMessage,
            null), cancellationToken, source.CaseId, source.CurrentRevision.RevisionId);
        cases.Add(copy);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return InitialRfqResult.From(copy);
    }
}
