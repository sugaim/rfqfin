using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class ConfirmedQuoteRepository(RfqDbContext dbContext)
    : IConfirmedQuoteRepository
{
    public void Add(ConfirmedQuote quote)
        => dbContext.ConfirmedQuotes.Add(ConfirmedQuoteMapper.ToEntity(quote));

    public async Task<ConfirmedQuote?> GetAsync(
        QuoteId quoteId,
        CancellationToken cancellationToken = default)
    {
        ConfirmedQuoteEntity? entity = await dbContext.ConfirmedQuotes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.QuoteId == quoteId.Value, cancellationToken);
        return entity is null ? null : ConfirmedQuoteMapper.ToDomain(entity);
    }
}

internal static class ConfirmedQuoteMapper
{
    internal static ConfirmedQuoteEntity ToEntity(ConfirmedQuote quote) => new()
    {
        QuoteId = quote.QuoteId.Value,
        RevisionId = quote.RevisionId.Value,
        SecurityId = quote.SecurityId.Value,
        SettlementDate = quote.SettlementDate,
        ConfirmedBy = quote.ConfirmedBy.Value,
        ConfirmedAt = quote.ConfirmedAt,
        Mode = quote.Mode,
        CalculatedPayloadJson = quote.Calculated is null
            ? null : QuotePayloadPersistence.Serialize(quote.Calculated),
        ManualPayloadJson = quote.Manual is null
            ? null : QuotePayloadPersistence.Serialize(quote.Manual),
        ExpiryMinutes = quote.ExpiryMinutes,
        ExpiresAt = quote.ExpiresAt,
        RequestReasonAnswered = quote.RequestReasonAnswered,
    };

    internal static ConfirmedQuote ToDomain(ConfirmedQuoteEntity entity) => ConfirmedQuote.Restore(
        new QuoteId(entity.QuoteId),
        new RevisionId(entity.RevisionId),
        SecurityId.Create(entity.SecurityId),
        entity.SettlementDate,
        UserId.Create(entity.ConfirmedBy),
        entity.ConfirmedAt,
        entity.Mode,
        entity.CalculatedPayloadJson is null ? null
            : QuotePayloadPersistence.DeserializeCalculated(entity.CalculatedPayloadJson),
        entity.ManualPayloadJson is null ? null
            : QuotePayloadPersistence.DeserializeManual(entity.ManualPayloadJson),
        entity.ExpiryMinutes,
        entity.ExpiresAt,
        entity.RequestReasonAnswered);
}
