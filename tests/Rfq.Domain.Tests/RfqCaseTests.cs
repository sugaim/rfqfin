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
            new DateOnly(2026, 9, 24),
            new DateOnly(2026, 9, 24),
            creator,
            createdAt);

        Assert.Equal(123, rfqCase.CaseId.Value);
        Assert.NotEqual(Guid.Empty, rfqCase.InitialRevision.RevisionId.Value);
        Assert.Equal(rfqCase.CaseId, rfqCase.InitialRevision.CaseId);
        Assert.Equal(rfqCase.InitialRevision.RevisionId, rfqCase.Lifecycle.CurrentRevisionId);
        Assert.Equal(RevisionStatus.Draft, rfqCase.InitialRevision.Status);
        Assert.Equal(RfqStatus.Draft, rfqCase.Status);
        Assert.Equal(creator, rfqCase.CreatedBy);
        Assert.Equal(creator, rfqCase.SalesId);
        Assert.Equal(creator, rfqCase.ContactOwnerId);
        Assert.Equal("trader-1", rfqCase.AssignedTraderId.Value);
        Assert.Equal("JGB", rfqCase.CategorySnapshot.Value);
        Assert.Equal(new DateOnly(2026, 9, 24), rfqCase.InitialRevision.SettlementDate);
        Assert.Equal(TimeSpan.Zero, rfqCase.CreatedAt.Offset);
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
}
