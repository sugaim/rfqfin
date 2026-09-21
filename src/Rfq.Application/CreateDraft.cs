using Rfq.Domain;

namespace Rfq.Application;

public sealed record CreateDraftCommand(string ClientId, string SecurityId);

public sealed record CreateDraftResult(
    long CaseId,
    Guid RevisionId,
    string RfqStatus,
    DateTimeOffset CreatedAt);

public sealed class CreateDraft(
    ICaseIdGenerator caseIdGenerator,
    IRfqCaseRepository rfqCases,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<CreateDraftResult> ExecuteAsync(
        CreateDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var clientId = ClientId.Create(command.ClientId);
        var securityId = SecurityId.Create(command.SecurityId);
        var caseId = await caseIdGenerator.NextAsync(cancellationToken);
        var rfqCase = RfqCase.CreateDraft(
            caseId,
            clientId,
            securityId,
            currentUser.User.UserId,
            timeProvider.GetUtcNow());

        rfqCases.Add(rfqCase);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateDraftResult(
            rfqCase.CaseId.Value,
            rfqCase.InitialRevision.RevisionId.Value,
            rfqCase.Status.ToString(),
            rfqCase.CreatedAt);
    }
}
