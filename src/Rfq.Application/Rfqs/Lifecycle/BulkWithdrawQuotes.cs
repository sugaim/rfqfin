using Rfq.Domain;

namespace Rfq.Application;

public sealed class BulkWithdrawQuotes(WithdrawQuote withdraw)
{
    public async Task<IReadOnlyList<LifecycleItemResult>> ExecuteAsync(
        IReadOnlyList<LifecycleItem> items,
        CancellationToken cancellationToken = default)
    {
        var results = new List<LifecycleItemResult>();
        foreach (var item in items)
        {
            try
            {
                await withdraw.ExecuteAsync(item.CaseId, item.ExpectedCurrentVersion, cancellationToken);
                results.Add(new(item.CaseId, "Withdrawn", null));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                or UnauthorizedAccessException or KeyNotFoundException
                or DomainRuleViolationException or DomainValidationException
                or StateVersionMismatchException)
            {
                results.Add(new(item.CaseId,
                    ex.Message.Contains("Presented", StringComparison.Ordinal) ? "Skipped" : "Failed",
                    ex.Message));
            }
        }
        return results;
    }
}
