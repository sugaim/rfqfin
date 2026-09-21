using Microsoft.Extensions.DependencyInjection;

namespace Rfq.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddRfqApplication(this IServiceCollection services)
    {
        services.AddSingleton<IRfqAuthorization, RfqAuthorization>();
        services.AddScoped<CreateDraft>();
        services.AddScoped<GetActiveSalesRfqs>();
        services.AddScoped<ResolveRfqCreationContext>();
        services.AddScoped<AssignedTraderValidator>();
        services.AddScoped<InitialRfqFactory>();
        services.AddScoped<UpdateInitialDraft>();
        services.AddScoped<ConfirmInitialDraft>();
        services.AddScoped<ConfirmNewRfq>();
        services.AddScoped<DiscardInitialDraft>();
        services.AddScoped<GetActiveTraderRfqs>();
        services.AddScoped<PickUpRfq>();
        services.AddScoped<ReleaseRfq>();
        services.AddScoped<AssignTrader>();
        services.AddScoped<TakeOverRfq>();
        services.AddScoped<CalculateWorkingQuote>();
        services.AddScoped<ChangeWorkingQuoteMode>();
        services.AddScoped<UpdateManualWorkingQuote>();
        services.AddScoped<ConfirmQuote>();
        services.AddScoped<PresentQuote>();
        services.AddScoped<UnpresentQuote>();
        services.AddScoped<CloseRfq>();
        services.AddScoped<BulkCloseRfqs>();
        services.AddScoped<CorrectRfqOutcome>();
        services.AddScoped<ChangeContactOwner>();
        services.AddScoped<UpdateSalesMemo>();
        services.AddScoped<UpdateTraderMemo>();
        services.AddScoped<SaveAmendment>();
        services.AddScoped<ConfirmAmendment>();
        services.AddScoped<DiscardAmendment>();
        services.AddScoped<BulkConfirmAmendments>();
        services.AddScoped<BulkDiscardAmendments>();
        services.AddScoped<CreateFromExisting>();
        services.AddScoped<WithdrawQuote>();
        services.AddScoped<BulkWithdrawQuotes>();
        services.AddScoped<CancelRfq>();
        services.AddScoped<ReopenRfq>();
        services.AddScoped<ExpireQuote>();
        services.AddScoped<ScratchPricer>();

        return services;
    }
}
