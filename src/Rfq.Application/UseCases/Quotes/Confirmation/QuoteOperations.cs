using Rfq.Domain;

namespace Rfq.Application;

public sealed record ConfirmQuoteItem(
    CaseId CaseId,
    QuoteExpiry Expiry,
    StateVersion ExpectedCurrentVersion,
    StateVersion ExpectedWorkingQuoteVersion);

public sealed class ConfirmQuotes(ConfirmQuote confirm, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CaseOperationResult>> ExecuteAsync(
        IReadOnlyList<ConfirmQuoteItem> items, CancellationToken cancellationToken = default) =>
        CaseOperation.ExecuteAsync(
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
                return CaseOperationOutcome.Applied;
            },
            unitOfWork,
            cancellationToken);
}

public sealed class WithdrawQuotes(WithdrawQuote withdraw, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CaseOperationResult>> ExecuteAsync(
        IReadOnlyList<LifecycleItem> items, CancellationToken cancellationToken = default) =>
        CaseOperation.ExecuteAsync(
            items,
            item => item.CaseId,
            async (item, token) =>
            {
                WithdrawQuoteResult result = await withdraw.ExecuteAsync(
                    item.CaseId, item.ExpectedCurrentVersion, token);
                return result.Outcome == WithdrawQuoteOutcome.Withdrawn
                    ? CaseOperationOutcome.Applied
                    : CaseOperationOutcome.NoChange;
            },
            unitOfWork,
            cancellationToken);
}
