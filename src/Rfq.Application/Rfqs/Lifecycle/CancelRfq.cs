using Rfq.Domain;

namespace Rfq.Application;

public sealed class CancelRfq(
    CancelRfqOperation operation,
    IUnitOfWork unitOfWork)
{
    public async Task<LifecycleResult> ExecuteAsync(
        CaseId caseId,
        StateVersion expectedVersion,
        CancellationToken cancellationToken = default)
    {
        LifecycleResult result = await operation.ApplyAsync(
            caseId,
            expectedVersion,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }
}
