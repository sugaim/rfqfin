using Rfq.Domain;

namespace Rfq.Application;

public sealed class CalculateWorkingQuote(
    IRfqCaseRepository rfqCases,
    IWorkingQuoteRepository workingQuotes,
    ICalculationClient calculationClient,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<WorkingQuoteResult> ExecuteAsync(
        CaseId caseId,
        CalculationDriver driver,
        decimal value,
        decimal simpleYieldSlide,
        StateVersion expectedCurrentVersion,
        StateVersion expectedWorkingQuoteVersion,
        CancellationToken cancellationToken = default)
    {
        var before = await GetContextAsync(caseId, cancellationToken);
        ValidateExpected(before, expectedCurrentVersion, expectedWorkingQuoteVersion);
        authorization.EnsureCanQuote(currentUser.User, ToAuthorizationState(before));

        var request = new CalculationRequest(
            Guid.NewGuid(),
            before.SecurityId,
            before.SettlementDate,
            driver,
            ToParameter(driver, value),
            simpleYieldSlide);
        var result = (await calculationClient.CalculateBulkAsync([request], cancellationToken))
            .Single(item => item.RequestId == request.RequestId);
        if (result is CalculationError error)
        {
            var failure = new CalculationFailureRecord(
                Guid.NewGuid(),
                caseId,
                before.RevisionId,
                currentUser.User.UserId,
                request.RequestId,
                driver,
                value,
                simpleYieldSlide,
                before.WorkingQuote,
                error.Code,
                error.Message,
                timeProvider.GetUtcNow());
            workingQuotes.AddFailure(failure);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new CalculationFailureException(
                failure.FailureLogId,
                error.Code,
                error.Message);
        }

        var success = (CalculationSuccess)result;
        var rfqCase = await OwnershipUseCase.LoadAsync(rfqCases, caseId, cancellationToken);
        if (rfqCase.CurrentRevision.RevisionId != before.RevisionId
            || rfqCase.Version != expectedCurrentVersion)
        {
            throw new StateVersionMismatchException(
                "The RFQ state changed while calculation was in progress.");
        }

        authorization.EnsureCanQuote(currentUser.User, ToAuthorizationState(rfqCase));
        var quote = await workingQuotes.GetAsync(
            rfqCase.CurrentRevision.RevisionId,
            cancellationToken)
            ?? throw new RfqInvariantException("WorkingQuote was not found.");
        quote = WorkingQuoteTransitions.ApplyCalculated(
            quote,
            success.Payload,
            expectedWorkingQuoteVersion,
            currentUser.User.UserId,
            timeProvider.GetUtcNow());
        workingQuotes.Update(quote);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkingQuoteResult.From(caseId, quote, rfqCase.Version);
    }

    private async Task<QuoteEditContext> GetContextAsync(
        CaseId caseId,
        CancellationToken cancellationToken) =>
        await workingQuotes.GetEditContextAsync(caseId, cancellationToken)
            ?? throw new RfqNotFoundException($"RFQ Case '{caseId}' was not found.");

    private static void ValidateExpected(
        QuoteEditContext context,
        StateVersion expectedCurrentVersion,
        StateVersion expectedWorkingQuoteVersion)
    {
        if (context.Version != expectedCurrentVersion)
        {
            throw new StateVersionMismatchException("The RFQ was changed by another user.");
        }

        if (context.WorkingQuote.Version != expectedWorkingQuoteVersion)
        {
            throw new StateVersionMismatchException("The WorkingQuote was changed by another user.");
        }
    }

    private static CalculationParameter ToParameter(
        CalculationDriver driver,
        decimal value) => driver switch
        {
            CalculationDriver.Price => new PriceCalculationParameter(value),
            CalculationDriver.BbgYield => new BbgYieldCalculationParameter(value),
            CalculationDriver.SimpleYield => new SimpleYieldCalculationParameter(value),
            CalculationDriver.GSpread => new GSpreadCalculationParameter(value),
            _ => throw new ArgumentOutOfRangeException(nameof(driver)),
        };

    private static QuoteAuthorizationState ToAuthorizationState(
        QuoteEditContext context) => new(
        true, context.AssignedTraderId, context.Ownership, context.QuoteState);

    internal static QuoteAuthorizationState ToAuthorizationState(RfqCase rfqCase) => new(
        rfqCase.Lifecycle is OpenRfq,
        rfqCase.AssignedTraderId,
        rfqCase.Ownership,
        (rfqCase.Lifecycle as ActiveRfq)?.QuoteState);
}
