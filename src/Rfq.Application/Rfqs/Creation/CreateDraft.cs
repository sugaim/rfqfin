using Rfq.Domain;

namespace Rfq.Application;

public sealed class CreateDraft(
    InitialRfqFactory initialRfqFactory,
    IRfqCaseRepository rfqCases,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<InitialRfqResult> ExecuteAsync(
        CreateDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        authorization.EnsureCanCreateRevision(currentUser.User);

        RfqCase rfqCase = await initialRfqFactory.CreateAsync(
            command,
            cancellationToken: cancellationToken);
        rfqCases.Add(rfqCase);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return InitialRfqResult.From(rfqCase);
    }
}
