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
            ClientId.Create("client-1"),
            SecurityId.Create("security-1"),
            creator,
            createdAt);

        Assert.NotEqual(Guid.Empty, rfqCase.CaseId.Value);
        Assert.NotEqual(Guid.Empty, rfqCase.InitialRevision.RevisionId.Value);
        Assert.Equal(rfqCase.CaseId, rfqCase.InitialRevision.CaseId);
        Assert.Equal(rfqCase.InitialRevision.RevisionId, rfqCase.Lifecycle.CurrentRevisionId);
        Assert.Equal(RevisionStatus.Draft, rfqCase.InitialRevision.Status);
        Assert.Equal(RfqStatus.Draft, rfqCase.Status);
        Assert.Equal(creator, rfqCase.CreatedBy);
        Assert.Equal(creator, rfqCase.SalesId);
        Assert.Equal(TimeSpan.Zero, rfqCase.CreatedAt.Offset);
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
