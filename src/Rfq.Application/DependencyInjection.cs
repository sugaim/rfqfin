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
        services.AddScoped<PresentRfq>();
        services.AddScoped<UnpresentRfq>();
        services.AddScoped<CloseHitRfq>();
        services.AddScoped<CloseAwayRfq>();
        services.AddScoped<CloseRfqOperation>();
        services.AddScoped<CorrectOutcomeToHit>();
        services.AddScoped<CorrectOutcomeToAway>();
        services.AddScoped<CorrectOutcomeOperation>();
        services.AddScoped<ChangeContactOwner>();
        services.AddScoped<ChangeContactOwners>();
        services.AddScoped<UpdateSalesMemo>();
        services.AddScoped<UpdateTraderMemo>();
        services.AddScoped<UpdateMemoOperation>();
        services.AddScoped<SaveAmendment>();
        services.AddScoped<StartAmendment>();
        services.AddScoped<ConfirmAmendment>();
        services.AddScoped<DiscardAmendment>();
        services.AddScoped<ConfirmAmendments>();
        services.AddScoped<DiscardAmendments>();
        services.AddScoped<ConfirmInitialDrafts>();
        services.AddScoped<DiscardInitialDrafts>();
        services.AddScoped<ConfirmQuotes>();
        services.AddScoped<CreateFromExisting>();
        services.AddScoped<WithdrawQuote>();
        services.AddScoped<WithdrawQuotes>();
        services.AddScoped<PresentRfqs>();
        services.AddScoped<UnpresentRfqs>();
        services.AddScoped<PickUpRfqs>();
        services.AddScoped<ReleaseRfqs>();
        services.AddScoped<AssignTraders>();
        services.AddScoped<TakeOverRfqs>();
        services.AddScoped<CloseAwayRfqs>();
        services.AddScoped<CancelRfq>();
        services.AddScoped<CancelRfqOperation>();
        services.AddScoped<CancelRfqs>();
        services.AddScoped<ReopenRfq>();
        services.AddScoped<ReopenRfqs>();
        services.AddScoped<ExpireQuote>();
        services.AddScoped<ScratchPrice>();
        services.AddScoped<GetPostProcessWorklist>();
        services.AddScoped<CommitPostProcessChanges>();

        return services;
    }
}
