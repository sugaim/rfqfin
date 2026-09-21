using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

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
            ?? throw new RfqInvariantException("WorkingQuote was not found.");
        var settlementDate = rfqCase.Current.CurrentRevision.SettlementDate
            ?? throw new RfqInvariantException("Confirmed Revision is missing SettlementDate.");

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
            CaseId = failure.CaseId.Value,
            RevisionId = failure.RevisionId.Value,
            TraderId = failure.TraderId.Value,
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
    public static WorkingQuote ToDomain(WorkingQuoteEntity entity)
    {
        try
        {
            return WorkingQuote.Restore(
                new RevisionId(entity.RevisionId),
                entity.Mode,
                entity.CalculatedPayloadJson is null ? null
                    : QuotePayloadPersistence.DeserializeCalculated(entity.CalculatedPayloadJson),
                entity.ManualPayloadJson is null ? null
                    : QuotePayloadPersistence.DeserializeManual(entity.ManualPayloadJson),
                new StateVersion(entity.Version),
                entity.CreatedAt,
                UserId.Create(entity.CreatedBy),
                entity.UpdatedAt,
                UserId.Create(entity.UpdatedBy));
        }
        catch (DomainValidationException exception)
        {
            throw new RfqInvariantException("Persisted WorkingQuote is invalid.", exception);
        }
    }

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
        entity.CalculatedPayloadJson = quote.Calculated is null
            ? null : QuotePayloadPersistence.Serialize(quote.Calculated);
        entity.ManualPayloadJson = quote.Manual is null
            ? null : QuotePayloadPersistence.Serialize(quote.Manual);
        entity.Version = quote.Version.Value;
        entity.UpdatedAt = quote.UpdatedAt;
        entity.UpdatedBy = quote.UpdatedBy.Value;
    }

}
