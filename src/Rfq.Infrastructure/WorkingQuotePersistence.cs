using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class WorkingQuoteEnsurer(RfqDbContext dbContext) : IWorkingQuoteEnsurer
{
    public async Task<WorkingQuote> EnsureAsync(
        RevisionId revisionId,
        RevisionId? quoteSeedRevisionId,
        UserId createdBy,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        var revisionValue = revisionId.Value;
        var existing = dbContext.WorkingQuotes.Local
            .SingleOrDefault(item => item.RevisionId == revisionValue)
            ?? await dbContext.WorkingQuotes.SingleOrDefaultAsync(
                item => item.RevisionId == revisionValue,
                cancellationToken);
        if (existing is not null)
        {
            return WorkingQuoteMapper.ToDomain(existing);
        }

        WorkingQuote quote;
        if (quoteSeedRevisionId is not null)
        {
            var seed = await dbContext.WorkingQuotes
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.RevisionId == quoteSeedRevisionId.Value.Value,
                    cancellationToken)
                ?? throw new KeyNotFoundException(
                    $"Quote seed Revision '{quoteSeedRevisionId.Value.Value}' was not found.");
            var source = WorkingQuoteMapper.ToDomain(seed);
            quote = WorkingQuote.Restore(
                revisionId, source.Mode, source.Calculated, source.Manual,
                new StateVersion(1), createdAt, createdBy, createdAt, createdBy);
        }
        else
        {
            quote = WorkingQuote.Restore(
                revisionId, WorkingQuoteMode.Calculated, null, null,
                new StateVersion(1), createdAt, createdBy, createdAt, createdBy);
        }

        dbContext.WorkingQuotes.Add(WorkingQuoteMapper.ToEntity(quote));
        return quote;
    }
}

public sealed class WorkingQuoteRepository(RfqDbContext dbContext) : IWorkingQuoteRepository
{
    public void Add(WorkingQuote workingQuote) =>
        dbContext.WorkingQuotes.Add(WorkingQuoteMapper.ToEntity(workingQuote));

    public async Task<QuoteEditContext?> GetEditContextAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default)
    {
        var rfqCase = await dbContext.RfqCases
            .AsNoTracking()
            .Include(item => item.Current)
            .ThenInclude(current => current.CurrentRevision)
            .SingleOrDefaultAsync(item => item.CaseId == caseId.Value, cancellationToken);
        if (rfqCase is null)
        {
            return null;
        }

        var quoteEntity = await dbContext.WorkingQuotes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.RevisionId == rfqCase.Current.CurrentRevisionId,
                cancellationToken)
            ?? throw new KeyNotFoundException("WorkingQuote was not found.");
        var settlementDate = rfqCase.Current.CurrentRevision.SettlementDate
            ?? throw new InvalidOperationException("Confirmed Revision is missing SettlementDate.");

        if (rfqCase.Current.Lifecycle != RfqLifecycleKind.Open
            || rfqCase.Current.RfqStatus != RfqStatus.Active)
            throw new DomainInvariantException("WorkingQuote context requires an Active RFQ.");
        var quoteState = rfqCase.Current.QuoteStatus switch
        {
            QuoteStatus.Requested => (ActiveQuoteState)new QuoteRequested(
                rfqCase.Current.QuoteRequestReason
                    ?? throw new DomainInvariantException("Requested RFQ is missing reason.")),
            QuoteStatus.Quoted => new QuoteConfirmed(new QuoteId(
                rfqCase.Current.CurrentQuoteId
                    ?? throw new DomainInvariantException("Quoted RFQ is missing quote id."))),
            _ => throw new DomainInvariantException("Open RFQ is missing quote state."),
        };
        return new QuoteEditContext(
            new CaseId(rfqCase.CaseId),
            new RevisionId(rfqCase.Current.CurrentRevisionId),
            SecurityId.Create(rfqCase.SecurityId),
            settlementDate,
            new StateVersion(rfqCase.Current.Version),
            UserId.Create(rfqCase.Current.AssignedTraderId),
            rfqCase.Current.Owned ? new Owned() : new Unowned(),
            quoteState,
            WorkingQuoteMapper.ToDomain(quoteEntity));
    }

    public async Task<WorkingQuote?> GetAsync(
        RevisionId revisionId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.WorkingQuotes.SingleOrDefaultAsync(
            item => item.RevisionId == revisionId.Value,
            cancellationToken);
        return entity is null ? null : WorkingQuoteMapper.ToDomain(entity);
    }

    public void Update(WorkingQuote workingQuote)
    {
        var entity = dbContext.WorkingQuotes.Local.SingleOrDefault(
            item => item.RevisionId == workingQuote.RevisionId.Value)
            ?? throw new InvalidOperationException(
                "The WorkingQuote must be loaded before it can be updated.");
        WorkingQuoteMapper.Apply(workingQuote, entity);
    }

    public void AddFailure(CalculationFailureRecord failure)
    {
        dbContext.CalculationFailureLogs.Add(new CalculationFailureLogEntity
        {
            FailureLogId = failure.FailureLogId,
            CaseId = failure.CaseId,
            RevisionId = failure.RevisionId,
            TraderId = failure.TraderId,
            RequestId = failure.RequestId,
            Driver = failure.Driver,
            AttemptedValue = failure.AttemptedValue,
            SimpleYieldSlide = failure.SimpleYieldSlide,
            PriorWorkingQuoteJson = JsonSerializer.Serialize(failure.PriorWorkingQuote),
            RequestJson = JsonSerializer.Serialize(new
            {
                failure.RequestId,
                failure.Driver,
                failure.AttemptedValue,
                failure.SimpleYieldSlide,
            }),
            ErrorCode = failure.ErrorCode,
            ErrorMessage = failure.ErrorMessage,
            OccurredAt = failure.OccurredAt.ToUniversalTime(),
        });
    }
}

internal static class WorkingQuoteMapper
{
    public static WorkingQuote ToDomain(WorkingQuoteEntity entity) => WorkingQuote.Restore(
        new RevisionId(entity.RevisionId),
        entity.Mode,
        Deserialize<CalculatedQuotePayload>(entity.CalculatedPayloadJson),
        Deserialize<ManualQuotePayload>(entity.ManualPayloadJson),
        new StateVersion(entity.Version),
        entity.CreatedAt,
        UserId.Create(entity.CreatedBy),
        entity.UpdatedAt,
        UserId.Create(entity.UpdatedBy));

    public static WorkingQuoteEntity ToEntity(WorkingQuote quote)
    {
        var entity = new WorkingQuoteEntity { RevisionId = quote.RevisionId.Value };
        Apply(quote, entity);
        entity.CreatedAt = quote.CreatedAt;
        entity.CreatedBy = quote.CreatedBy.Value;
        return entity;
    }

    public static void Apply(WorkingQuote quote, WorkingQuoteEntity entity)
    {
        entity.Mode = quote.Mode;
        entity.CalculatedPayloadJson = Serialize(quote.Calculated);
        entity.ManualPayloadJson = Serialize(quote.Manual);
        entity.Version = quote.Version.Value;
        entity.UpdatedAt = quote.UpdatedAt;
        entity.UpdatedBy = quote.UpdatedBy.Value;
    }

    private static string? Serialize<T>(T? value) where T : class =>
        value is null ? null : JsonSerializer.Serialize(value);

    private static T? Deserialize<T>(string? value) where T : class =>
        value is null ? null : JsonSerializer.Deserialize<T>(value);
}
