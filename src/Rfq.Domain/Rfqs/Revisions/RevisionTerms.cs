namespace Rfq.Domain;

public sealed class RevisionTerms
{
    public RevisionTerms(
        decimal? notional,
        DateOnly? settlementDate,
        DateOnly standardSettlementDate,
        string? salesAndTradingMessage)
    {
        Notional = notional;
        SettlementDate = settlementDate;
        StandardSettlementDate = standardSettlementDate;
        SalesAndTradingMessage = salesAndTradingMessage?.Trim() ?? string.Empty;
    }

    public decimal? Notional { get; }
    public DateOnly? SettlementDate { get; }
    public DateOnly StandardSettlementDate { get; }
    public string SalesAndTradingMessage { get; }
}
