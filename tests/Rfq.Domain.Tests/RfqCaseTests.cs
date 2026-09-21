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
