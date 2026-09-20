using Rfq.Domain;

namespace Rfq.Application;

public sealed record CreateDraftCommand(string ClientId, string SecurityId);

public sealed record CreateDraftResult(
    Guid CaseId,
    Guid RevisionId,
    string RfqStatus,
    DateTimeOffset CreatedAt);

public sealed class CreateDraft(
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

        var rfqCase = RfqCase.CreateDraft(
            ClientId.Create(command.ClientId),
            SecurityId.Create(command.SecurityId),
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
