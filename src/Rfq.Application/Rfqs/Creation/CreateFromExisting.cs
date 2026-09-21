using Rfq.Domain;

namespace Rfq.Application;

public sealed class CreateFromExisting(
    IRfqCaseRepository cases,
    InitialRfqFactory factory,
    ResolveRfqCreationContext resolveCreationContext,
    IBusinessDateProvider businessDate,
    IDeskLocalDateResolver deskLocalDateResolver,
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
        var today = await businessDate.GetCurrentAsync(cancellationToken);
        var sourceBusinessDate = await deskLocalDateResolver.ResolveAsync(
            source.CreatedAt, currentUser.User.DeskId, cancellationToken);
        var context = await resolveCreationContext.ExecuteAsync(
            source.SecurityId, cancellationToken);
        var settlement = sourceBusinessDate == today
            ? source.CurrentRevision.SettlementDate ?? context.StandardSettlementDate
            : context.StandardSettlementDate;
        var copy = await factory.CreateAsync(new CreateDraftCommand(
            source.ClientId,
            source.SecurityId,
            source.CurrentRevision.Notional,
            settlement,
            context.StandardSettlementDate,
            source.CurrentRevision.SalesAndTradingMessage,
            context.DefaultAssignedTraderId), cancellationToken,
            source.CaseId, source.CurrentRevision.RevisionId);
        cases.Add(copy);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return InitialRfqResult.From(copy);
    }
}
