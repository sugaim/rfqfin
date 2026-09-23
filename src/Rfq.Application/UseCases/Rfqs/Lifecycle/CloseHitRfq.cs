using Rfq.Domain;

namespace Rfq.Application;

public sealed class CloseHitRfq(
    CloseRfqOperation operation,
    IUnitOfWork unitOfWork)
{
    public async Task<CloseRfqResult> ExecuteAsync(
        CaseId caseId,
        StateVersion expectedCurrentVersion,
        CancellationToken cancellationToken = default)
    {
        CloseRfqResult result = await operation.ApplyAsync(
            caseId,
            RfqStatus.Hit,
            expectedCurrentVersion,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }
}
