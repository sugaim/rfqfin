using Xunit;

namespace Rfq.Domain.Tests;

public sealed class ConfirmedQuoteTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CalculatedSnapshotIsImmutableWhenWorkingQuoteChangesLater()
    {
        var trader = UserId.Create("trader-a");
        var working = WorkingQuote.CreateEmpty(RevisionId.New(), trader, Now);
        working.ApplyCalculated(Calculated(99.5m), working.Version, trader, Now);

        var confirmed = ConfirmedQuote.Create(
            QuoteId.New(),
            working,
            SecurityId.Create("security-1"),
            new DateOnly(2026, 9, 24),
            trader,
            Now,
            5,
            QuoteRequestReason.Initial);
        working.ApplyCalculated(Calculated(98m), working.Version, trader, Now.AddMinutes(1));

        Assert.Equal(99.5m, confirmed.Calculated?.Price);
        Assert.Equal(Now.AddMinutes(5), confirmed.ExpiresAt);
        Assert.Equal(5, confirmed.ExpiryMinutes);
    }

    [Fact]
    public void ManualSnapshotRequiresBothIndependentValuesAndExcludesCalculatedPayload()
    {
        var trader = UserId.Create("trader-a");
        var working = WorkingQuote.CreateEmpty(RevisionId.New(), trader, Now);
        working.ApplyCalculated(Calculated(99.5m), working.Version, trader, Now);
        working.SwitchMode(WorkingQuoteMode.Manual, working.Version, trader, Now);

        Assert.Throws<InvalidOperationException>(() => ConfirmedQuote.Create(
            QuoteId.New(),
            working,
            SecurityId.Create("security-1"),
            new DateOnly(2026, 9, 24),
            trader,
            Now,
            null,
            QuoteRequestReason.Initial));

        working.UpdateManual(98.75m, 1.25m, working.Version, trader, Now);
        var confirmed = ConfirmedQuote.Create(
            QuoteId.New(),
            working,
            SecurityId.Create("security-1"),
            new DateOnly(2026, 9, 24),
            trader,
            Now,
            null,
            QuoteRequestReason.Initial);

        Assert.Null(confirmed.Calculated);
        Assert.Equal(98.75m, confirmed.Manual?.Price);
        Assert.Equal(1.25m, confirmed.Manual?.FinalSimpleYield);
    }

    [Fact]
    public void ConfirmPresentAndUnpresentPreserveCurrentQuoteIdentity()
    {
        var rfqCase = CreateOwnedCase();
        var quoteId = QuoteId.New();

        rfqCase.ConfirmQuote(
            quoteId,
            rfqCase.InitialRevision.RevisionId,
            rfqCase.CurrentVersion);
        Assert.Equal(RfqStatus.Active, rfqCase.Status);
        Assert.Equal(QuoteStatus.Quoted, rfqCase.QuoteStatus);
        Assert.Null(rfqCase.QuoteRequestReason);

        Assert.Throws<InvalidOperationException>(() => rfqCase.ConfirmQuote(
            QuoteId.New(),
            rfqCase.InitialRevision.RevisionId,
            rfqCase.CurrentVersion));

        rfqCase.Present(rfqCase.CurrentVersion);
        Assert.Equal(RfqStatus.Presented, rfqCase.Status);
        Assert.Equal(quoteId, rfqCase.CurrentQuoteId);

        rfqCase.Unpresent(rfqCase.CurrentVersion);
        Assert.Equal(RfqStatus.Active, rfqCase.Status);
        Assert.Equal(QuoteStatus.Quoted, rfqCase.QuoteStatus);
        Assert.Equal(quoteId, rfqCase.CurrentQuoteId);
    }

    [Fact]
    public void PresentRequiresCurrentValidConfirmedQuote()
    {
        var rfqCase = CreateOwnedCase();

        Assert.Throws<InvalidOperationException>(() =>
            rfqCase.Present(rfqCase.CurrentVersion));
    }

    private static RfqCase CreateOwnedCase()
    {
        var sales = UserId.Create("sales-dev");
        var rfqCase = RfqCase.CreateDraft(
            new CaseId(20),
            ClientId.Create("client-1"),
            SecurityId.Create("security-1"),
            CategoryId.Create("JGB"),
            UserId.Create("trader-a"),
            100_000_000m,
            new DateOnly(2026, 9, 24),
            new DateOnly(2026, 9, 23),
            null,
            sales,
            Now);
        rfqCase.ConfirmInitial(
            new DateOnly(2026, 9, 21),
            sales,
            Now,
            rfqCase.InitialRevision.Version);
        rfqCase.PickUp(UserId.Create("trader-a"), rfqCase.CurrentVersion);
        return rfqCase;
    }

    private static CalculatedQuotePayload Calculated(decimal price) => new(
        CalculationDriver.Price,
        price,
        price,
        0.8m,
        0.81m,
        0.03m,
        0.84m,
        0.83m,
        5m,
        8m);
}
