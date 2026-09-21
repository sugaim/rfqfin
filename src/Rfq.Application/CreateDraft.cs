using Rfq.Domain;

namespace Rfq.Application;

public sealed record CreateDraftCommand(
    string ClientId,
    string SecurityId,
    decimal? Notional,
    DateOnly? SettlementDate,
    string? SalesAndTradingMessage,
    string? AssignedTraderId);

public sealed record InitialRfqResult(
    long CaseId,
    Guid RevisionId,
    string RfqStatus,
    string RevisionStatus,
    string? QuoteStatus,
    string? QuoteRequestReason,
    string CategoryId,
    string ContactOwnerId,
    string AssignedTraderId,
    decimal? Notional,
    DateOnly? SettlementDate,
    DateOnly StandardSettlementDate,
    string SalesAndTradingMessage,
    long Version,
    DateTimeOffset CreatedAt)
{
    public static InitialRfqResult From(RfqCase rfqCase) => new(
        rfqCase.CaseId.Value,
        rfqCase.InitialRevision.RevisionId.Value,
        rfqCase.Status.ToString(),
        rfqCase.InitialRevision.Status.ToString(),
        rfqCase.QuoteStatus?.ToString(),
        rfqCase.QuoteRequestReason?.ToString(),
        rfqCase.CategorySnapshot.Value,
        rfqCase.ContactOwnerId.Value,
        rfqCase.AssignedTraderId.Value,
        rfqCase.InitialRevision.Notional,
        rfqCase.InitialRevision.SettlementDate,
        rfqCase.InitialRevision.StandardSettlementDate,
        rfqCase.InitialRevision.SalesAndTradingMessage,
        rfqCase.InitialRevision.Version,
        rfqCase.CreatedAt);
}

public sealed class CreateDraft(
    InitialRfqFactory initialRfqFactory,
    IRfqCaseRepository rfqCases,
    IUnitOfWork unitOfWork)
{
    public async Task<InitialRfqResult> ExecuteAsync(
        CreateDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rfqCase = await initialRfqFactory.CreateAsync(command, cancellationToken);
        rfqCases.Add(rfqCase);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return InitialRfqResult.From(rfqCase);
    }
}
