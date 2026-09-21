using Rfq.Domain;

namespace Rfq.Application;

public sealed class ExpireQuote(
    IRfqCaseRepository cases,
    IQuoteEventSink events,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<bool> ExecuteAsync(ExpiredQuoteCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        var rfq = await cases.GetAsync(new CaseId(candidate.CaseId), cancellationToken);
        if (rfq is null)
        {
            return false;
        }
        try
        {
            rfq = QuoteTransitions.Expire(
                rfq, new QuoteId(candidate.QuoteId),
                new StateVersion(candidate.CurrentVersion));
        }
        catch (DomainRuleViolationException)
        {
            return false;
        }
        cases.Update(rfq);
        events.Record(new(QuoteTransitionKind.Expired, candidate.QuoteId,
            "system", timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
