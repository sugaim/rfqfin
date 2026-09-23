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
        services.AddScoped<CloseHitRfq>();
        services.AddScoped<CloseAwayRfq>();
        services.AddScoped<CloseRfqOperation>();
        services.AddScoped<CorrectOutcomeToHit>();
        services.AddScoped<CorrectOutcomeToAway>();
        services.AddScoped<CorrectOutcomeOperation>();
        services.AddScoped<ChangeContactOwner>();
        services.AddScoped<UpdateSalesMemo>();
        services.AddScoped<UpdateTraderMemo>();
        services.AddScoped<UpdateMemoOperation>();
        services.AddScoped<SaveAmendment>();
        services.AddScoped<StartAmendment>();
        services.AddScoped<ConfirmAmendment>();
        services.AddScoped<DiscardAmendment>();
        services.AddScoped<BulkConfirmAmendments>();
        services.AddScoped<BulkDiscardAmendments>();
        services.AddScoped<BulkConfirmInitialDrafts>();
        services.AddScoped<BulkDiscardInitialDrafts>();
        services.AddScoped<BulkConfirmQuotes>();
        services.AddScoped<CreateFromExisting>();
        services.AddScoped<WithdrawQuote>();
        services.AddScoped<BulkWithdrawQuotes>();
        services.AddScoped<BulkPresentQuotes>();
        services.AddScoped<BulkUnpresentQuotes>();
        services.AddScoped<BulkPickUpRfqs>();
        services.AddScoped<BulkReleaseRfqs>();
        services.AddScoped<BulkAssignTrader>();
        services.AddScoped<BulkCloseAwayRfqs>();
        services.AddScoped<CancelRfq>();
        services.AddScoped<CancelRfqOperation>();
        services.AddScoped<BulkCancelRfqs>();
        services.AddScoped<ReopenRfq>();
        services.AddScoped<ExpireQuote>();
        services.AddScoped<ScratchPricer>();
        services.AddScoped<GetPostProcessWorklist>();
        services.AddScoped<CommitPostProcessChanges>();

        return services;
    }
}
