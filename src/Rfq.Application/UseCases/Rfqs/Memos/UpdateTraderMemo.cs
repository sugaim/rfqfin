using Rfq.Domain;

namespace Rfq.Application;

public sealed class UpdateTraderMemo(
    UpdateMemoOperation operation,
    IUnitOfWork unitOfWork)
{
    public async Task<MemoResult> ExecuteAsync(
        CaseId caseId,
        string? memo,
        StateVersion expectedVersion,
        CancellationToken cancellationToken = default)
    {
        MemoResult result = await operation.ApplyTraderAsync(
            caseId,
            memo,
            expectedVersion,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }
}
