using Rfq.Domain;

namespace Rfq.Application;

internal static class AmendmentBulk
{
    public static async Task<IReadOnlyList<AmendmentItemResult>> ExecuteAsync(
        IReadOnlyList<AmendmentItem> items,
        Func<AmendmentItem, CancellationToken, Task<AmendmentResult>> action,
        string success,
        CancellationToken cancellationToken)
    {
        var results = new List<AmendmentItemResult>(items.Count);
        foreach (var item in items)
        {
            try
            {
                await action(item, cancellationToken);
                results.Add(new(item.CaseId, success, null));
            }
            catch (Exception exception) when (exception is ArgumentException
                or InvalidOperationException or KeyNotFoundException
                or UnauthorizedAccessException or DomainRuleViolationException
                or DomainValidationException or StateVersionMismatchException)
            {
                results.Add(new(item.CaseId, "Failed", exception.Message));
            }
        }
        return results;
    }
}
