using Xunit;

namespace Rfq.Domain.Tests;

public sealed class RfqCaseTests
{
    [Fact]
    public void CreateDraftCreatesCaseIdentityAndInitialDraftRevision()
    {
        var creator = UserId.Create("sales-1");
        var createdAt = new DateTimeOffset(2026, 9, 21, 1, 2, 3, TimeSpan.FromHours(9));

        var rfqCase = RfqCase.CreateDraft(
            new CaseId(123),
            ClientId.Create("client-1"),
            SecurityId.Create("security-1"),
            CategoryId.Create("JGB"),
            UserId.Create("trader-1"),
            100_000_000m,
            new DateOnly(2026, 9, 24),
            new DateOnly(2026, 9, 24),
            "Customer inquiry",
            creator,
            createdAt);

        Assert.Equal(123, rfqCase.CaseId.Value);
        Assert.NotEqual(Guid.Empty, rfqCase.InitialRevision.RevisionId.Value);
        Assert.Equal(rfqCase.CaseId, rfqCase.InitialRevision.CaseId);
        Assert.Equal(
            rfqCase.InitialRevision.RevisionId,
            Assert.IsType<DraftRfq>(rfqCase.Lifecycle).CurrentRevisionId);
        Assert.Equal(RevisionStatus.Draft, rfqCase.InitialRevision.Status);
        Assert.Equal(RfqStatus.Draft, rfqCase.Status);
        Assert.Equal(creator, rfqCase.CreatedBy);
        Assert.Equal(creator, rfqCase.SalesId);
        Assert.Equal(creator, rfqCase.ContactOwnerId);
        Assert.Equal("trader-1", rfqCase.AssignedTraderId.Value);
        Assert.Equal("JGB", rfqCase.CategorySnapshot.Value);
        Assert.Equal(new DateOnly(2026, 9, 24), rfqCase.InitialRevision.SettlementDate);
        Assert.Equal(100_000_000m, rfqCase.InitialRevision.Notional);
        Assert.Equal(TimeSpan.Zero, rfqCase.CreatedAt.Offset);
    }

    [Fact]
    public void ConfirmInitialMovesDraftToOpenRequestedInitial()
    {
        var creator = UserId.Create("sales-1");
        var rfqCase = CreateValidDraft(creator);

        rfqCase.ConfirmInitial(
            new DateOnly(2026, 9, 21),
            creator,
            new DateTimeOffset(2026, 9, 21, 1, 0, 0, TimeSpan.Zero),
            1);

        var open = Assert.IsType<OpenRfq>(rfqCase.Lifecycle);
        Assert.Equal(RfqStatus.Active, rfqCase.Status);
        Assert.Equal(QuoteStatus.Requested, open.QuoteStatus);
        Assert.Equal(QuoteRequestReason.Initial, open.QuoteRequestReason);
        Assert.Equal(RevisionStatus.Confirmed, rfqCase.InitialRevision.Status);
        Assert.Equal(2, rfqCase.InitialRevision.Version);
    }

    [Theory]
    [InlineData(null, "2026-09-24")]
    [InlineData(0, "2026-09-24")]
    [InlineData(100, "2026-09-20")]
    public void ConfirmInitialRejectsInvalidRequiredFields(
        int? notional,
        string settlementDateText)
    {
        var creator = UserId.Create("sales-1");
        var rfqCase = RfqCase.CreateDraft(
            new CaseId(123),
            ClientId.Create("client-1"),
            SecurityId.Create("security-1"),
            CategoryId.Create("JGB"),
            UserId.Create("trader-1"),
            notional is null ? null : (decimal)notional.Value,
            DateOnly.Parse(settlementDateText),
            new DateOnly(2026, 9, 24),
            null,
            creator,
            DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => rfqCase.ConfirmInitial(
            new DateOnly(2026, 9, 21),
            creator,
            DateTimeOffset.UtcNow,
            1));
    }

    [Fact]
    public void RequestedOpenRfqRequiresRequestReason()
    {
        Assert.Throws<ArgumentException>(() => new OpenRfq(
            RevisionId.New(),
            UserId.Create("sales-1"),
            UserId.Create("trader-1"),
            QuoteStatus.Requested,
            null,
            false));
    }

    [Fact]
    public void OwnershipOperationsPreserveAssignedOwnerInvariant()
    {
        var traderOne = UserId.Create("trader-1");
        var traderTwo = UserId.Create("trader-2");
        var rfqCase = CreateValidDraft(UserId.Create("sales-1"));
        rfqCase.ConfirmInitial(
            new DateOnly(2026, 9, 21),
            UserId.Create("sales-1"),
            DateTimeOffset.UtcNow,
            rfqCase.InitialRevision.Version);

        rfqCase.PickUp(traderOne, 2);
        Assert.True(rfqCase.Owned);
        Assert.Equal(traderOne, rfqCase.AssignedTraderId);

        rfqCase.Release(traderOne, 3);
        Assert.False(rfqCase.Owned);
        Assert.Equal(traderOne, rfqCase.AssignedTraderId);

        rfqCase.AssignTo(traderTwo, 4);
        Assert.False(rfqCase.Owned);
        Assert.Equal(traderTwo, rfqCase.AssignedTraderId);

        rfqCase.PickUp(traderTwo, 5);
        rfqCase.TakeOver(traderOne, 6);
        Assert.True(rfqCase.Owned);
        Assert.Equal(traderOne, rfqCase.AssignedTraderId);
        Assert.Equal(7, rfqCase.CurrentVersion);
        Assert.Equal(traderOne, Assert.IsType<OpenRfq>(rfqCase.Lifecycle).AssignedTraderId);
    }

    [Fact]
    public void OwnershipChangeRejectsStaleCurrentVersion()
    {
        var rfqCase = CreateValidDraft(UserId.Create("sales-1"));
        rfqCase.ConfirmInitial(
            new DateOnly(2026, 9, 21),
            UserId.Create("sales-1"),
            DateTimeOffset.UtcNow,
            rfqCase.InitialRevision.Version);

        Assert.Throws<InvalidOperationException>(() =>
            rfqCase.PickUp(UserId.Create("trader-1"), 1));
    }

    [Fact]
    public void CloseRequiresCurrentQuoteAndRetainsItAsClosedQuote()
    {
        var rfqCase = CreateValidDraft(UserId.Create("sales-1"));
        rfqCase.ConfirmInitial(
            new DateOnly(2026, 9, 21),
            UserId.Create("sales-1"),
            DateTimeOffset.UtcNow,
            rfqCase.InitialRevision.Version);

        Assert.Throws<InvalidOperationException>(() =>
            rfqCase.Close(RfqStatus.Hit, rfqCase.CurrentVersion));

        rfqCase.PickUp(UserId.Create("trader-1"), rfqCase.CurrentVersion);
        var quoteId = QuoteId.New();
        rfqCase.ConfirmQuote(
            quoteId,
            rfqCase.InitialRevision.RevisionId,
            rfqCase.CurrentVersion);
        rfqCase.Close(RfqStatus.Hit, rfqCase.CurrentVersion);

        var closed = Assert.IsType<ClosedRfq>(rfqCase.Lifecycle);
        Assert.Equal(RfqStatus.Hit, rfqCase.Status);
        Assert.Equal(quoteId, closed.ClosedQuoteId);
        Assert.Equal(quoteId, rfqCase.ClosedQuoteId);
        Assert.Null(rfqCase.CurrentQuoteId);
        Assert.Null(rfqCase.QuoteStatus);
        Assert.False(rfqCase.Owned);
    }

    [Fact]
    public void CloseDiscardsPendingDraftAndOutcomeCorrectionIsExplicit()
    {
        var sales = UserId.Create("sales-1");
        var original = CreateValidDraft(sales);
        original.ConfirmInitial(
            new DateOnly(2026, 9, 21),
            sales,
            DateTimeOffset.UtcNow,
            original.InitialRevision.Version);
        var quoteId = QuoteId.New();
        original.ConfirmQuote(
            quoteId,
            original.InitialRevision.RevisionId,
            original.CurrentVersion);
        var pending = RfqRevision.Restore(
            RevisionId.New(),
            original.CaseId,
            RevisionStatus.Draft,
            120_000_000m,
            new DateOnly(2026, 9, 25),
            new DateOnly(2026, 9, 23),
            "pending amendment",
            null,
            null,
            1,
            DateTimeOffset.UtcNow,
            sales,
            null,
            null);
        var rfqCase = RfqCase.Restore(
            original.CaseId,
            original.ClientId,
            original.SecurityId,
            original.CategorySnapshot,
            original.CreatedAt,
            original.CreatedBy,
            original.SalesId,
            original.ContactOwnerId,
            original.AssignedTraderId,
            original.Owned,
            original.CurrentVersion,
            original.InitialRevision,
            original.Lifecycle,
            pending);

        rfqCase.Close(RfqStatus.Away, rfqCase.CurrentVersion);

        Assert.Equal(RevisionStatus.Discarded, pending.Status);
        Assert.Throws<InvalidOperationException>(() =>
            rfqCase.CorrectOutcome(RfqStatus.Away, rfqCase.CurrentVersion));
        rfqCase.CorrectOutcome(RfqStatus.Hit, rfqCase.CurrentVersion);
        Assert.Equal(RfqStatus.Hit, rfqCase.Status);
        Assert.Equal(quoteId, rfqCase.ClosedQuoteId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CaseIdRejectsNonPositiveValue(long value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaseId(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ClientIdRejectsMissingValue(string value)
    {
        Assert.Throws<ArgumentException>(() => ClientId.Create(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SecurityIdRejectsMissingValue(string value)
    {
        Assert.Throws<ArgumentException>(() => SecurityId.Create(value));
    }

    [Fact]
    public void AmendmentKeepsLiveQuoteUntilConfirmedThenSupersedes()
    {
        var sales = UserId.Create("sales-1");
        var rfq = CreateValidDraft(sales);
        rfq.ConfirmInitial(new DateOnly(2026, 9, 21), sales, DateTimeOffset.UtcNow, 1);
        var originalRevision = rfq.CurrentRevision;
        var quoteId = QuoteId.New();
        rfq.ConfirmQuote(quoteId, originalRevision.RevisionId, rfq.CurrentVersion);

        var draft = rfq.SaveAmendmentDraft(125_000_000m,
            new DateOnly(2026, 9, 25), "revised", sales, DateTimeOffset.UtcNow,
            rfq.CurrentVersion);

        Assert.Equal(quoteId, rfq.CurrentQuoteId);
        Assert.Equal(originalRevision, rfq.CurrentRevision);
        rfq.ConfirmAmendment(new DateOnly(2026, 9, 21), sales,
            DateTimeOffset.UtcNow, rfq.CurrentVersion, draft.Version);
        Assert.Equal(RevisionStatus.Superseded, originalRevision.Status);
        Assert.Equal(RevisionStatus.Confirmed, rfq.CurrentRevision.Status);
        Assert.Null(rfq.CurrentQuoteId);
        Assert.Equal(QuoteRequestReason.Revised, rfq.QuoteRequestReason);
    }

    [Fact]
    public void DiscardAmendmentPreservesOperationalState()
    {
        var sales = UserId.Create("sales-1");
        var rfq = CreateValidDraft(sales);
        rfq.ConfirmInitial(new DateOnly(2026, 9, 21), sales, DateTimeOffset.UtcNow, 1);
        var quoteId = QuoteId.New();
        rfq.ConfirmQuote(quoteId, rfq.CurrentRevision.RevisionId, rfq.CurrentVersion);
        var draft = rfq.SaveAmendmentDraft(125_000_000m,
            new DateOnly(2026, 9, 25), "revised", sales, DateTimeOffset.UtcNow,
            rfq.CurrentVersion);

        rfq.DiscardAmendment(rfq.CurrentVersion, draft.Version);

        Assert.Equal(RevisionStatus.Discarded, draft.Status);
        Assert.Equal(quoteId, rfq.CurrentQuoteId);
        Assert.Equal(QuoteStatus.Quoted, rfq.QuoteStatus);
    }

    [Fact]
    public void WithdrawCancelReopenAndExpiryFollowLifecycleRules()
    {
        var sales = UserId.Create("sales-1");
        var rfq = CreateValidDraft(sales);
        rfq.ConfirmInitial(new DateOnly(2026, 9, 21), sales, DateTimeOffset.UtcNow, 1);
        var firstQuote = QuoteId.New();
        rfq.ConfirmQuote(firstQuote, rfq.CurrentRevision.RevisionId, rfq.CurrentVersion);
        rfq.WithdrawQuote(rfq.CurrentVersion);
        Assert.Equal(QuoteRequestReason.Withdrawn, rfq.QuoteRequestReason);
        rfq.Cancel(rfq.CurrentVersion);
        Assert.Equal(RfqStatus.Cancelled, rfq.Status);
        rfq.Reopen(rfq.CurrentVersion);
        Assert.Equal(QuoteRequestReason.Reopened, rfq.QuoteRequestReason);
        var secondQuote = QuoteId.New();
        rfq.ConfirmQuote(secondQuote, rfq.CurrentRevision.RevisionId, rfq.CurrentVersion);
        Assert.True(rfq.ExpireQuote(secondQuote, rfq.CurrentVersion));
        Assert.False(rfq.ExpireQuote(secondQuote, rfq.CurrentVersion));
        Assert.Equal(QuoteRequestReason.Expired, rfq.QuoteRequestReason);
    }

    private static RfqCase CreateValidDraft(UserId creator) => RfqCase.CreateDraft(
        new CaseId(123),
        ClientId.Create("client-1"),
        SecurityId.Create("security-1"),
        CategoryId.Create("JGB"),
        UserId.Create("trader-1"),
        100_000_000m,
        new DateOnly(2026, 9, 24),
        new DateOnly(2026, 9, 24),
        "Customer inquiry",
        creator,
        DateTimeOffset.UtcNow);
}
