using System.ComponentModel.DataAnnotations;
using Rfq.Domain;

namespace Rfq.Api.RfqQuotes;

public enum QuoteExpiryType { None, After }
public enum QuoteMode { Calculated, Manual }
public enum CalculationDriverValue { Price, BbgYield, SimpleYield, GSpread }

public sealed record QuoteExpiryRequest(
    [Required] QuoteExpiryType? Type,
    [Range(1, int.MaxValue)] int? Minutes);

public sealed record QuoteExpiryResponse(QuoteExpiryType Type, int? Minutes);

public sealed record CalculatedQuoteResponse(
    CalculationDriverValue Driver, decimal DriverValue, decimal Price,
    decimal BbgYield, decimal BaseSimpleYield, decimal SimpleYieldSlide,
    decimal FinalSimpleYield, decimal InternalYield, decimal GSpread, decimal Asw);

public sealed record ManualQuoteResponse(decimal? Price, decimal? FinalSimpleYield);

public static class QuoteApiMapper
{
    public static QuoteExpiry ToDomain(QuoteExpiryRequest request) => request.Type switch
    {
        QuoteExpiryType.None when request.Minutes is null => new QuoteExpiry.None(),
        QuoteExpiryType.After when request.Minutes is > 0 =>
            new QuoteExpiry.After(TimeSpan.FromMinutes(request.Minutes.Value)),
        QuoteExpiryType.None => throw new ArgumentException(
            "A none expiry must not include minutes.", nameof(request)),
        QuoteExpiryType.After => throw new ArgumentException(
            "An after expiry requires positive integer minutes.", nameof(request)),
        _ => throw new ArgumentException("Unknown Quote Expiry type.", nameof(request)),
    };

    public static QuoteExpiryResponse ToApi(QuoteExpiry expiry) => expiry switch
    {
        QuoteExpiry.None => new(QuoteExpiryType.None, null),
        QuoteExpiry.After after => new(QuoteExpiryType.After,
            checked((int)after.Duration.TotalMinutes)),
        _ => throw new InvalidOperationException("Unknown Quote Expiry policy."),
    };

    public static CalculatedQuoteResponse? ToApi(CalculatedQuotePayload? payload) =>
        payload is null ? null : new(
            payload.Driver switch
            {
                CalculationDriver.Price => CalculationDriverValue.Price,
                CalculationDriver.BbgYield => CalculationDriverValue.BbgYield,
                CalculationDriver.SimpleYield => CalculationDriverValue.SimpleYield,
                CalculationDriver.GSpread => CalculationDriverValue.GSpread,
                _ => throw new InvalidOperationException("Unknown Calculation Driver."),
            },
            payload.DriverValue, payload.Price, payload.BbgYield, payload.BaseSimpleYield,
            payload.SimpleYieldSlide, payload.FinalSimpleYield, payload.InternalYield,
            payload.GSpread, payload.Asw);

    public static ManualQuoteResponse? ToApi(ManualQuotePayload? payload) =>
        payload is null ? null : new(payload.Price, payload.FinalSimpleYield);

    public static CalculationDriver ToDomain(CalculationDriverValue driver) => driver switch
    {
        CalculationDriverValue.Price => CalculationDriver.Price,
        CalculationDriverValue.BbgYield => CalculationDriver.BbgYield,
        CalculationDriverValue.SimpleYield => CalculationDriver.SimpleYield,
        CalculationDriverValue.GSpread => CalculationDriver.GSpread,
        _ => throw new ArgumentException("Unknown Calculation Driver.", nameof(driver)),
    };

    public static WorkingQuoteMode ToDomain(QuoteMode mode) => mode switch
    {
        QuoteMode.Calculated => WorkingQuoteMode.Calculated,
        QuoteMode.Manual => WorkingQuoteMode.Manual,
        _ => throw new ArgumentException("Unknown Quote Mode.", nameof(mode)),
    };

    public static QuoteMode ToApi(WorkingQuoteMode mode) => mode switch
    {
        WorkingQuoteMode.Calculated => QuoteMode.Calculated,
        WorkingQuoteMode.Manual => QuoteMode.Manual,
        _ => throw new InvalidOperationException("Unknown Quote Mode."),
    };
}
