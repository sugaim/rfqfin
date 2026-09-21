using Xunit;

namespace Rfq.Domain.Tests;

public sealed class WorkingQuoteTests
{
    private static readonly UserId Trader = UserId.Create("trader-a");
    private static readonly DateTimeOffset Now =
        new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ManualModeClearsManualValuesAndRetainsCalculatedPayload()
    {
        var quote = WorkingQuote.CreateEmpty(RevisionId.New(), Trader, Now);
        var calculated = Payload();
        quote.ApplyCalculated(calculated, 1, Trader, Now);

        quote.SwitchMode(WorkingQuoteMode.Manual, 2, Trader, Now);
        Assert.Equal(WorkingQuoteMode.Manual, quote.Mode);
        Assert.Equal(calculated, quote.Calculated);
        Assert.Equal(new ManualQuotePayload(null, null), quote.Manual);

        quote.UpdateManual(99.5m, 1.25m, 3, Trader, Now);
        quote.SwitchMode(WorkingQuoteMode.Calculated, 4, Trader, Now);
        Assert.Equal(WorkingQuoteMode.Calculated, quote.Mode);
        Assert.Equal(calculated, quote.Calculated);
        Assert.Equal(new ManualQuotePayload(99.5m, 1.25m), quote.Manual);
    }

    [Fact]
    public void UpdateRejectsStaleWorkingQuoteVersion()
    {
        var quote = WorkingQuote.CreateEmpty(RevisionId.New(), Trader, Now);
        quote.ApplyCalculated(Payload(), 1, Trader, Now);

        Assert.Throws<InvalidOperationException>(() =>
            quote.SwitchMode(WorkingQuoteMode.Manual, 1, Trader, Now));
    }

    private static CalculatedQuotePayload Payload() => new(
        CalculationDriver.Price,
        100m,
        100m,
        1m,
        1.01m,
        0.02m,
        1.03m,
        1.04m,
        10m,
        13m);
}
