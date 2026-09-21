using Rfq.Domain;

namespace Rfq.Application;

public sealed record PastRfqSearch(
    DateOnly? From = null, DateOnly? To = null, string? ClientId = null,
    string? SecurityId = null, string? CategoryId = null, string? ContactOwnerId = null,
    string? SalesId = null, string? AssignedTraderId = null, string? Status = null,
    long? CaseId = null);

public sealed record PastRfqItem(
    long CaseId, DateTimeOffset CreatedAt, string ClientId, string ClientName,
    string SecurityId, string SecurityName, string CategoryId, string Status,
    string? QuoteStatus, string ContactOwnerId, string SalesId, string AssignedTraderId,
    decimal? Notional, DateOnly? SettlementDate);

public sealed record PastRfqResult(IReadOnlyList<PastRfqItem> Items, bool RequiresNarrowing);
public sealed record RevisionHistoryItem(Guid RevisionId, string Status, decimal? Notional,
    DateOnly? SettlementDate, string Message, long Version, DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt, Guid? CopiedFromRevisionId, Guid? QuoteSeedRevisionId);
public sealed record QuoteHistoryItem(Guid QuoteId, Guid RevisionId, string Mode,
    DateTimeOffset ConfirmedAt, DateTimeOffset? ExpiresAt, string RequestReason);
public sealed record EodSummaryItem(string ContactOwnerId, int Open, int Hit, int Away);
public sealed record GridConfig(string ScreenId, string ConfigKey, int Version,
    string ConfigJson, DateTimeOffset UpdatedAt);

public interface IOperationalQueries
{
    Task<PastRfqResult> SearchAsync(PastRfqSearch search, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RevisionHistoryItem>> GetRevisionHistoryAsync(long caseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuoteHistoryItem>> GetQuoteHistoryAsync(long caseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EodSummaryItem>> GetEodAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<GridConfig?> GetGridConfigAsync(string screenId, string configKey, CancellationToken cancellationToken = default);
    Task<GridConfig> SaveGridConfigAsync(string screenId, string configKey, int version, string configJson, CancellationToken cancellationToken = default);
}

public sealed record ScratchPriceRequest(string SecurityId, DateOnly SettlementDate,
    CalculationDriver Driver, decimal Value, decimal SimpleYieldSlide);

public sealed class ScratchPricer(ICalculationClient calculationClient)
{
    public async Task<CalculatedQuotePayload> ExecuteAsync(ScratchPriceRequest input,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        CalculationParameter parameter = input.Driver switch
        {
            CalculationDriver.Price => new PriceCalculationParameter(input.Value),
            CalculationDriver.BbgYield => new BbgYieldCalculationParameter(input.Value),
            CalculationDriver.SimpleYield => new SimpleYieldCalculationParameter(input.Value),
            CalculationDriver.GSpread => new GSpreadCalculationParameter(input.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(input)),
        };
        var result = (await calculationClient.CalculateBulkAsync([
            new(id, input.SecurityId, input.SettlementDate, input.Driver, parameter, input.SimpleYieldSlide)
        ], cancellationToken)).Single();
        return result switch
        {
            CalculationSuccess success => success.Payload,
            CalculationError error => throw new CalculationFailureException(id, error.Code, error.Message),
            _ => throw new InvalidOperationException("Unknown calculation response."),
        };
    }
}
