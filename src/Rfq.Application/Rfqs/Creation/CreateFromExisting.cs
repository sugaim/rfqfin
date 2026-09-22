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
        RfqCase source = await ClosedRfqUseCase.LoadAsync(cases, sourceCaseId, cancellationToken);
        DateOnly today = await businessDate.GetCurrentAsync(cancellationToken);
        DateOnly sourceBusinessDate = await deskLocalDateResolver.ResolveAsync(
            source.CreatedAt, currentUser.User.DeskId, cancellationToken);
        RfqCreationContext context = await resolveCreationContext.ExecuteAsync(
            source.SecurityId, cancellationToken);
        DateOnly settlement = sourceBusinessDate == today
            ? source.CurrentRevision.SettlementDate ?? context.StandardSettlementDate
            : context.StandardSettlementDate;
        RfqCase copy = await factory.CreateAsync(
            new CreateDraftCommand(
                source.ClientId,
                source.SecurityId,
                source.CurrentRevision.Notional,
                settlement,
                context.StandardSettlementDate,
                source.CurrentRevision.SalesAndTradingMessage,
                context.DefaultAssignedTraderId),
            source.CaseId,
            source.CurrentRevision.RevisionId,
            cancellationToken);
        cases.Add(copy);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return InitialRfqResult.From(copy);
    }
}
