using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class MockStandardSettlementResolver : IStandardSettlementResolver
{
    public DateOnly Resolve(SecurityId securityId, DateOnly businessDate)
    {
        ArgumentNullException.ThrowIfNull(securityId);

        var result = businessDate;
        for (var businessDays = 0; businessDays < 2;)
        {
            result = result.AddDays(1);
            if (result.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                businessDays++;
            }
        }

        return result;
    }
}
