using Rfq.Domain;

namespace Rfq.Application;

public sealed class ChangeWorkingQuoteMode(
    IRfqCaseRepository rfqCases,
    IWorkingQuoteRepository workingQuotes,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<WorkingQuoteResult> ExecuteAsync(
        long caseId,
        WorkingQuoteMode mode,
        long expectedCurrentVersion,
        long expectedWorkingQuoteVersion,
        CancellationToken cancellationToken = default)
    {
        var (rfqCase, quote) = await WorkingQuoteMutation.LoadAsync(
            rfqCases,
            workingQuotes,
            authorization,
            currentUser.User,
            caseId,
            expectedCurrentVersion,
            expectedWorkingQuoteVersion,
            cancellationToken);
        quote = WorkingQuoteTransitions.SwitchMode(
            quote,
            mode,
            new StateVersion(expectedWorkingQuoteVersion),
            currentUser.User.UserId,
            timeProvider.GetUtcNow());
        workingQuotes.Update(quote);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkingQuoteResult.From(caseId, quote, rfqCase.Version.Value);
    }
}
