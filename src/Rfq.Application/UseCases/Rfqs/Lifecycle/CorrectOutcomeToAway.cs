using Rfq.Domain;

namespace Rfq.Application;

public sealed class CorrectOutcomeToAway(
    CorrectOutcomeOperation operation,
    IUnitOfWork unitOfWork)
{
    public async Task<CloseRfqResult> ExecuteAsync(
        CaseId caseId,
        string? reason,
        StateVersion expectedCurrentVersion,
        CancellationToken cancellationToken = default)
    {
        CloseRfqResult result = await operation.ApplyAsync(
            caseId,
            reason,
            expectedCurrentVersion,
            RfqStatus.Away,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }
}
