namespace Rfq.Infrastructure;

internal static class DeskDateBoundary
{
    public static DateTimeOffset ToUtc(DateOnly localDate, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var localMidnight = DateTime.SpecifyKind(
            localDate.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(localMidnight))
        {
            throw new InvalidOperationException(
                $"Local midnight {localDate:yyyy-MM-dd} does not exist in timezone '{timeZone.Id}'.");
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, timeZone));
    }
}
