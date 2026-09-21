using Rfq.Domain;

namespace Rfq.Application;

public interface IWorkingQuoteRepository
{
    void Add(WorkingQuote workingQuote);

    Task<QuoteEditContext?> GetEditContextAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default);

    Task<WorkingQuote?> GetAsync(
        RevisionId revisionId,
        CancellationToken cancellationToken = default);

    void Update(WorkingQuote workingQuote);

    void AddFailure(CalculationFailureRecord failure);
}
