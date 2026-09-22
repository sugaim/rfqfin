using System.Text.Json;
using Rfq.Domain;
using Xunit;

namespace Rfq.Infrastructure.Tests;

public sealed class QuotePersistenceContractTests
{
    private static readonly CalculatedQuotePayload Calculated = new(
        CalculationDriver.GSpread, 12.5m, 100.25m, .8m, .81m, .02m,
        .83m, .84m, 12.5m, 15m, 13.5m, 14.5m, 13.5m);
    private static readonly ManualQuotePayload Manual = new(100.25m, .83m);

    [Fact]
    public void Current_v1_calculated_json_reads_into_current_domain_payload()
    {
        const string json = """
            {"type":"calculated-v1","driver":"g-spread","driverValue":12.5,"price":100.25,"bbgYield":0.8,"baseSimpleYield":0.81,"simpleYieldSlide":0.02,"finalSimpleYield":0.83,"internalYield":0.84,"gSpread":12.5,"asw":15,"ysc":13.5,"iSpread":14.5,"zSpread":13.5}
            """;

        Assert.Equal(Calculated, QuotePayloadPersistence.DeserializeCalculated(json));
    }

    [Fact]
    public void Current_payload_writes_and_reads_explicit_latest_variant_version()
    {
        var calculatedJson = QuotePayloadPersistence.Serialize(Calculated);
        var manualJson = QuotePayloadPersistence.Serialize(Manual);

        using var calculatedDocument = JsonDocument.Parse(calculatedJson);
        using var manualDocument = JsonDocument.Parse(manualJson);
        Assert.Equal(QuotePayloadPersistence.CalculatedV1,
            calculatedDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal(QuotePayloadPersistence.ManualV1,
            manualDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal(Calculated, QuotePayloadPersistence.DeserializeCalculated(calculatedJson));
        Assert.Equal(Manual, QuotePayloadPersistence.DeserializeManual(manualJson));
    }

    [Theory]
    [InlineData("{\"type\":\"calculated-v2\"}")]
    [InlineData("not-json")]
    [InlineData("{\"type\":\"calculated-v1\"}")]
    public void Unknown_malformed_or_incomplete_calculated_payload_fails(string json)
    {
        Assert.Throws<DomainInvariantException>(
            () => QuotePayloadPersistence.DeserializeCalculated(json));
    }

    [Fact]
    public void Working_quote_mapper_round_trips_versioned_payload()
    {
        var entity = WorkingEntity(QuotePayloadPersistence.Serialize(Calculated));

        var domain = WorkingQuoteMapper.ToDomain(entity);
        var persisted = WorkingQuoteMapper.ToEntity(domain);

        Assert.Equal(Calculated, domain.Calculated);
        Assert.Equal(Calculated,
            QuotePayloadPersistence.DeserializeCalculated(persisted.CalculatedPayloadJson!));
        Assert.Contains("\"type\":\"calculated-v1\"", persisted.CalculatedPayloadJson);
    }

    [Fact]
    public void Confirmed_quote_mapper_round_trips_versioned_payload()
    {
        var entity = new ConfirmedQuoteEntity
        {
            QuoteId = Guid.NewGuid(),
            RevisionId = Guid.NewGuid(),
            SecurityId = "security-a",
            SettlementDate = new DateOnly(2026, 9, 23),
            ConfirmedBy = "trader-a",
            ConfirmedAt = new DateTimeOffset(2026, 9, 21, 1, 0, 0, TimeSpan.Zero),
            Mode = WorkingQuoteMode.Manual,
            ManualPayloadJson = QuotePayloadPersistence.Serialize(Manual),
            RequestReasonAnswered = QuoteRequestReason.Initial,
        };

        var domain = ConfirmedQuoteMapper.ToDomain(entity);
        var persisted = ConfirmedQuoteMapper.ToEntity(domain);

        Assert.Equal(Manual, domain.Manual);
        Assert.Equal(Manual, QuotePayloadPersistence.DeserializeManual(persisted.ManualPayloadJson!));
        Assert.Contains("\"type\":\"manual-v1\"", persisted.ManualPayloadJson);
    }

    private static WorkingQuoteEntity WorkingEntity(string calculatedJson) => new()
    {
        RevisionId = Guid.NewGuid(),
        Version = 3,
        Mode = WorkingQuoteMode.Calculated,
        CalculatedPayloadJson = calculatedJson,
        CreatedAt = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero),
        CreatedBy = "trader-a",
        UpdatedAt = new DateTimeOffset(2026, 9, 21, 1, 0, 0, TimeSpan.Zero),
        UpdatedBy = "trader-a",
    };
}
