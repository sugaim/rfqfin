using Rfq.Domain;

namespace Rfq.Application;

public sealed record ConfirmQuoteItem(
    CaseId CaseId,
    QuoteExpiry Expiry,
    StateVersion ExpectedCurrentVersion,
    StateVersion ExpectedWorkingQuoteVersion);

public sealed class BulkConfirmQuotes(ConfirmQuote confirm, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<BulkItemResult>> ExecuteAsync(
        IReadOnlyList<ConfirmQuoteItem> items, CancellationToken cancellationToken = default) =>
        BulkOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                await confirm.ExecuteAsync(
                    item.CaseId,
                    item.Expiry,
                    item.ExpectedCurrentVersion,
                    item.ExpectedWorkingQuoteVersion,
                    token);
                return BulkActionOutcome.Succeeded;
            },
            unitOfWork,
            cancellationToken);
}

public sealed class BulkWithdrawQuotes(WithdrawQuote withdraw, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<BulkItemResult>> ExecuteAsync(
        IReadOnlyList<LifecycleItem> items, CancellationToken cancellationToken = default) =>
        BulkOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                WithdrawQuoteResult result = await withdraw.ExecuteAsync(
                    item.CaseId, item.ExpectedCurrentVersion, token);
                return result.Outcome == WithdrawQuoteOutcome.Withdrawn
                    ? BulkActionOutcome.Succeeded
                    : BulkActionOutcome.Skipped;
            },
            unitOfWork,
            cancellationToken);
}
