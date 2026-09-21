using Xunit;

namespace Rfq.Infrastructure.Tests;

public sealed class BusinessDateBoundaryTests
{
    [Fact]
    public void Jst_calendar_day_boundaries_are_converted_to_utc()
    {
        var jst = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

        var from = DeskDateBoundary.ToUtc(new DateOnly(2026, 9, 21), jst);
        var to = DeskDateBoundary.ToUtc(new DateOnly(2026, 9, 22), jst);

        Assert.Equal(new DateTimeOffset(2026, 9, 20, 15, 0, 0, TimeSpan.Zero), from);
        Assert.Equal(new DateTimeOffset(2026, 9, 21, 15, 0, 0, TimeSpan.Zero), to);
    }
}
