using Rfq.Domain;

namespace Rfq.Infrastructure;

internal static class QuotePayloadPersistence
{
    internal const string CalculatedV1 = "calculated-v1";
    internal const string ManualV1 = "manual-v1";

    internal static string Serialize(CalculatedQuotePayload payload) =>
        PersistenceJsonSerializer.Serialize(new CalculatedQuotePayloadDtoV1
        {
            Type = CalculatedV1,
            Driver = DriverCode(payload.Driver),
            DriverValue = payload.DriverValue,
            Price = payload.Price,
            BbgYield = payload.BbgYield,
            BaseSimpleYield = payload.BaseSimpleYield,
            SimpleYieldSlide = payload.SimpleYieldSlide,
            FinalSimpleYield = payload.FinalSimpleYield,
            InternalYield = payload.InternalYield,
            GSpread = payload.GSpread,
            Asw = payload.Asw,
            Ysc = payload.Ysc,
            ISpread = payload.ISpread,
            ZSpread = payload.ZSpread,
        });

    internal static string Serialize(ManualQuotePayload payload) =>
        PersistenceJsonSerializer.Serialize(new ManualQuotePayloadDtoV1
        {
            Type = ManualV1,
            Price = payload.Price,
            FinalSimpleYield = payload.FinalSimpleYield,
        });

    internal static CalculatedQuotePayload DeserializeCalculated(string json)
    {
        using var document = PersistenceJsonSerializer.Parse(json, "calculated quote payload");
        if (!document.RootElement.TryGetProperty("type", out var discriminator))
            throw new DomainInvariantException(
                "Persisted calculated quote payload type is missing.");
        if (discriminator.ValueKind != System.Text.Json.JsonValueKind.String
            || discriminator.GetString() != CalculatedV1)
            throw new DomainInvariantException("Persisted calculated quote payload has an unsupported type.");
        return From(PersistenceJsonSerializer.Deserialize<CalculatedQuotePayloadDtoV1>(
            json, "calculated quote payload"));
    }

    internal static ManualQuotePayload DeserializeManual(string json)
    {
        using var document = PersistenceJsonSerializer.Parse(json, "manual quote payload");
        if (!document.RootElement.TryGetProperty("type", out var discriminator))
            throw new DomainInvariantException(
                "Persisted manual quote payload type is missing.");
        if (discriminator.ValueKind != System.Text.Json.JsonValueKind.String
            || discriminator.GetString() != ManualV1)
            throw new DomainInvariantException("Persisted manual quote payload has an unsupported type.");
        var dto = PersistenceJsonSerializer.Deserialize<ManualQuotePayloadDtoV1>(
            json, "manual quote payload");
        return new ManualQuotePayload(dto.Price, dto.FinalSimpleYield);
    }

    private static CalculatedQuotePayload From(CalculatedQuotePayloadDtoV1 dto) => new(
        ParseDriver(dto.Driver), dto.DriverValue, dto.Price, dto.BbgYield,
        dto.BaseSimpleYield, dto.SimpleYieldSlide, dto.FinalSimpleYield,
        dto.InternalYield, dto.GSpread, dto.Asw, dto.Ysc, dto.ISpread, dto.ZSpread);

    private static string DriverCode(CalculationDriver driver) => driver switch
    {
        CalculationDriver.Price => "price",
        CalculationDriver.BbgYield => "bbg-yield",
        CalculationDriver.SimpleYield => "simple-yield",
        CalculationDriver.Ysc => "ysc",
        CalculationDriver.GSpread => "g-spread",
        CalculationDriver.Asw => "asw",
        CalculationDriver.ISpread => "i-spread",
        CalculationDriver.ZSpread => "z-spread",
        _ => throw new DomainInvariantException($"Unsupported calculation driver '{driver}'."),
    };

    private static CalculationDriver ParseDriver(string value) => value switch
    {
        "price" => CalculationDriver.Price,
        "bbg-yield" => CalculationDriver.BbgYield,
        "simple-yield" => CalculationDriver.SimpleYield,
        "ysc" => CalculationDriver.Ysc,
        "g-spread" => CalculationDriver.GSpread,
        "asw" => CalculationDriver.Asw,
        "i-spread" => CalculationDriver.ISpread,
        "z-spread" => CalculationDriver.ZSpread,
        _ => throw new DomainInvariantException(
            $"Persisted calculated quote driver '{value}' is invalid."),
    };

}

internal sealed class CalculatedQuotePayloadDtoV1
{
    public required string Type { get; init; }
    public required string Driver { get; init; }
    public required decimal DriverValue { get; init; }
    public required decimal Price { get; init; }
    public required decimal BbgYield { get; init; }
    public required decimal BaseSimpleYield { get; init; }
    public required decimal SimpleYieldSlide { get; init; }
    public required decimal FinalSimpleYield { get; init; }
    public required decimal InternalYield { get; init; }
    public required decimal GSpread { get; init; }
    public required decimal Asw { get; init; }
    public required decimal Ysc { get; init; }
    public required decimal ISpread { get; init; }
    public required decimal ZSpread { get; init; }
}

internal sealed class ManualQuotePayloadDtoV1
{
    public required string Type { get; init; }
    public required decimal? Price { get; init; }
    public required decimal? FinalSimpleYield { get; init; }
}
