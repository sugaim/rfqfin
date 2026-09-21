using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class ConfirmedQuoteRepository(RfqDbContext dbContext)
    : IConfirmedQuoteRepository
{
    public void Add(ConfirmedQuote quote)
    {
        dbContext.ConfirmedQuotes.Add(new ConfirmedQuoteEntity
        {
            QuoteId = quote.QuoteId.Value,
            RevisionId = quote.RevisionId.Value,
            SecurityId = quote.SecurityId.Value,
            SettlementDate = quote.SettlementDate,
            ConfirmedBy = quote.ConfirmedBy.Value,
            ConfirmedAt = quote.ConfirmedAt,
            Mode = quote.Mode,
            CalculatedPayloadJson = Serialize(quote.Calculated),
            ManualPayloadJson = Serialize(quote.Manual),
            ExpiryMinutes = quote.ExpiryMinutes,
            ExpiresAt = quote.ExpiresAt,
            RequestReasonAnswered = quote.RequestReasonAnswered,
        });
    }

    public async Task<ConfirmedQuote?> GetAsync(
        QuoteId quoteId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ConfirmedQuotes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.QuoteId == quoteId.Value, cancellationToken);
        return entity is null ? null : ConfirmedQuote.Restore(
            new QuoteId(entity.QuoteId),
            new RevisionId(entity.RevisionId),
            SecurityId.Create(entity.SecurityId),
            entity.SettlementDate,
            UserId.Create(entity.ConfirmedBy),
            entity.ConfirmedAt,
            entity.Mode,
            Deserialize<CalculatedQuotePayload>(entity.CalculatedPayloadJson),
            Deserialize<ManualQuotePayload>(entity.ManualPayloadJson),
            entity.ExpiryMinutes,
            entity.ExpiresAt,
            entity.RequestReasonAnswered);
    }

    private static string? Serialize<T>(T? value) where T : class =>
        value is null ? null : JsonSerializer.Serialize(value);

    private static T? Deserialize<T>(string? value) where T : class =>
        value is null ? null : JsonSerializer.Deserialize<T>(value);
}

public sealed class PersistedEventSink : IQuoteEventSink, IRfqEventSink
{
    private readonly List<PendingEvent> pending = [];

    internal IReadOnlyList<PendingEvent> Pending => pending;

    public void Record(QuoteTransition transition)
    {
        pending.Add(new PendingQuoteEvent(
            transition.QuoteId,
            transition.Kind,
            transition.PerformedBy,
            transition.OccurredAt,
            JsonSerializer.Serialize(new
            {
                transition.Kind,
                QuoteId = transition.QuoteId.Value,
                PerformedBy = transition.PerformedBy.Value,
                transition.OccurredAt,
            })));
    }

    public void Record(RfqTransition transition)
    {
        pending.Add(new PendingRfqEvent(
            transition.CaseId,
            transition.Kind,
            transition.PerformedBy,
            transition.OccurredAt,
            JsonSerializer.Serialize(new
            {
                transition.Kind,
                CaseId = transition.CaseId.Value,
                PerformedBy = transition.PerformedBy.Value,
                transition.OccurredAt,
                QuoteId = transition.QuoteId?.Value,
                transition.From,
                transition.To,
                transition.Reason,
            })));
    }

    internal void Clear() => pending.Clear();
}

internal abstract record PendingEvent(
    UserId ActorUserId,
    DateTimeOffset OccurredAt,
    string PayloadJson);

internal sealed record PendingRfqEvent(
    CaseId CaseId,
    RfqTransitionKind Type,
    UserId ActorUserId,
    DateTimeOffset OccurredAt,
    string PayloadJson)
    : PendingEvent(ActorUserId, OccurredAt, PayloadJson);

internal sealed record PendingQuoteEvent(
    QuoteId QuoteId,
    QuoteTransitionKind Type,
    UserId ActorUserId,
    DateTimeOffset OccurredAt,
    string PayloadJson)
    : PendingEvent(ActorUserId, OccurredAt, PayloadJson);

public sealed class CaseMemoRepository(RfqDbContext dbContext) : ICaseMemoRepository
{
    public async Task<CaseMemo?> GetAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.CaseMemos
            .SingleOrDefaultAsync(item => item.CaseId == caseId.Value, cancellationToken);
        return entity is null
            ? null
            : CaseMemo.Restore(
                new CaseId(entity.CaseId),
                entity.SalesMemo,
                entity.TraderMemo,
                new StateVersion(entity.Version));
    }

    public void Update(CaseMemo memo)
    {
        var entity = dbContext.CaseMemos.Local
            .SingleOrDefault(item => item.CaseId == memo.CaseId.Value)
            ?? throw new InvalidOperationException(
                "The Case Memo must be loaded before it can be updated.");
        entity.SalesMemo = memo.SalesMemo;
        entity.TraderMemo = memo.TraderMemo;
        entity.Version = memo.Version.Value;
    }
}
