using Rfq.Domain;

namespace Rfq.Application;

public abstract record CalculationParameter(decimal Value);

public sealed record PriceCalculationParameter(decimal Price)
    : CalculationParameter(Price);

public sealed record BbgYieldCalculationParameter(decimal Yield)
    : CalculationParameter(Yield);

public sealed record SimpleYieldCalculationParameter(decimal Yield)
    : CalculationParameter(Yield);

public sealed record GSpreadCalculationParameter(decimal Spread)
    : CalculationParameter(Spread);

public sealed record CalculationRequest(
    Guid RequestId,
    string SecurityId,
    DateOnly SettlementDate,
    CalculationDriver Driver,
    CalculationParameter Parameter,
    decimal SimpleYieldSlide);

public abstract record CalculationResult(Guid RequestId);

public sealed record CalculationSuccess(
    Guid RequestId,
    CalculatedQuotePayload Payload) : CalculationResult(RequestId);

public sealed record CalculationError(
    Guid RequestId,
    string Code,
    string Message) : CalculationResult(RequestId);

public interface ICalculationClient
{
    Task<IReadOnlyList<CalculationResult>> CalculateBulkAsync(
        IReadOnlyList<CalculationRequest> requests,
        CancellationToken cancellationToken = default);
}

public sealed record QuoteEditContext(
    long CaseId,
    Guid RevisionId,
    string SecurityId,
    DateOnly SettlementDate,
    long CurrentVersion,
    string AssignedTraderId,
    bool Owned,
    string QuoteStatus,
    WorkingQuote WorkingQuote);

public sealed record CalculationFailureRecord(
    Guid FailureLogId,
    long CaseId,
    Guid RevisionId,
    string TraderId,
    Guid RequestId,
    CalculationDriver Driver,
    decimal AttemptedValue,
    decimal SimpleYieldSlide,
    WorkingQuote PriorWorkingQuote,
    string ErrorCode,
    string ErrorMessage,
    DateTimeOffset OccurredAt);

public interface IWorkingQuoteRepository
{
    Task<QuoteEditContext?> GetEditContextAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default);

    Task<WorkingQuote?> GetAsync(
        RevisionId revisionId,
        CancellationToken cancellationToken = default);

    void Update(WorkingQuote workingQuote);

    void AddFailure(CalculationFailureRecord failure);
}

public sealed class CalculationFailureException(
    Guid failureLogId,
    string code,
    string message) : Exception(message)
{
    public Guid FailureLogId { get; } = failureLogId;

    public string Code { get; } = code;
}

public sealed record WorkingQuoteResult(
    long CaseId,
    Guid RevisionId,
    string Mode,
    CalculatedQuotePayload? Calculated,
    ManualQuotePayload? Manual,
    long Version,
    long CurrentVersion)
{
    public static WorkingQuoteResult From(
        long caseId,
        WorkingQuote quote,
        long currentVersion) => new(
        caseId,
        quote.RevisionId.Value,
        quote.Mode.ToString(),
        quote.Calculated,
        quote.Manual,
        quote.Version,
        currentVersion);
}

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
        long caseId,
        CalculationDriver driver,
        decimal value,
        decimal simpleYieldSlide,
        long expectedCurrentVersion,
        long expectedWorkingQuoteVersion,
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
                currentUser.User.UserId.Value,
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
        if (rfqCase.InitialRevision.RevisionId.Value != before.RevisionId
            || rfqCase.CurrentVersion != expectedCurrentVersion)
        {
            throw new InvalidOperationException(
                "The RFQ state changed while calculation was in progress.");
        }

        authorization.EnsureCanQuote(currentUser.User, ToAuthorizationState(rfqCase));
        var quote = await workingQuotes.GetAsync(
            rfqCase.InitialRevision.RevisionId,
            cancellationToken)
            ?? throw new KeyNotFoundException("WorkingQuote was not found.");
        quote.ApplyCalculated(
            success.Payload,
            expectedWorkingQuoteVersion,
            currentUser.User.UserId,
            timeProvider.GetUtcNow());
        workingQuotes.Update(quote);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkingQuoteResult.From(caseId, quote, rfqCase.CurrentVersion);
    }

    private async Task<QuoteEditContext> GetContextAsync(
        long caseId,
        CancellationToken cancellationToken) =>
        await workingQuotes.GetEditContextAsync(new CaseId(caseId), cancellationToken)
            ?? throw new KeyNotFoundException($"RFQ Case '{caseId}' was not found.");

    private static void ValidateExpected(
        QuoteEditContext context,
        long expectedCurrentVersion,
        long expectedWorkingQuoteVersion)
    {
        if (context.CurrentVersion != expectedCurrentVersion)
        {
            throw new InvalidOperationException("The RFQ was changed by another user.");
        }

        if (context.WorkingQuote.Version != expectedWorkingQuoteVersion)
        {
            throw new InvalidOperationException("The WorkingQuote was changed by another user.");
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
        true,
        UserId.Create(context.AssignedTraderId),
        context.Owned,
        context.QuoteStatus);

    internal static QuoteAuthorizationState ToAuthorizationState(RfqCase rfqCase) => new(
        rfqCase.Lifecycle is OpenRfq,
        rfqCase.AssignedTraderId,
        rfqCase.Owned,
        rfqCase.QuoteStatus?.ToString());
}

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
        quote.SwitchMode(
            mode,
            expectedWorkingQuoteVersion,
            currentUser.User.UserId,
            timeProvider.GetUtcNow());
        workingQuotes.Update(quote);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkingQuoteResult.From(caseId, quote, rfqCase.CurrentVersion);
    }
}

public sealed class UpdateManualWorkingQuote(
    IRfqCaseRepository rfqCases,
    IWorkingQuoteRepository workingQuotes,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<WorkingQuoteResult> ExecuteAsync(
        long caseId,
        decimal? price,
        decimal? finalSimpleYield,
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
        quote.UpdateManual(
            price,
            finalSimpleYield,
            expectedWorkingQuoteVersion,
            currentUser.User.UserId,
            timeProvider.GetUtcNow());
        workingQuotes.Update(quote);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return WorkingQuoteResult.From(caseId, quote, rfqCase.CurrentVersion);
    }
}

internal static class WorkingQuoteMutation
{
    public static async Task<(RfqCase Case, WorkingQuote Quote)> LoadAsync(
        IRfqCaseRepository rfqCases,
        IWorkingQuoteRepository workingQuotes,
        IRfqAuthorization authorization,
        CurrentUser user,
        long caseId,
        long expectedCurrentVersion,
        long expectedWorkingQuoteVersion,
        CancellationToken cancellationToken)
    {
        var rfqCase = await OwnershipUseCase.LoadAsync(rfqCases, caseId, cancellationToken);
        if (rfqCase.CurrentVersion != expectedCurrentVersion)
        {
            throw new InvalidOperationException("The RFQ was changed by another user.");
        }

        authorization.EnsureCanQuote(user, CalculateWorkingQuote.ToAuthorizationState(rfqCase));
        var quote = await workingQuotes.GetAsync(
            rfqCase.InitialRevision.RevisionId,
            cancellationToken)
            ?? throw new KeyNotFoundException("WorkingQuote was not found.");
        if (quote.Version != expectedWorkingQuoteVersion)
        {
            throw new InvalidOperationException("The WorkingQuote was changed by another user.");
        }

        return (rfqCase, quote);
    }
}
