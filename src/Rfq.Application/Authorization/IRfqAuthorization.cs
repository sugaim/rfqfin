using Rfq.Domain;

namespace Rfq.Application;

public interface IRfqAuthorization
{
    void EnsureCanViewTraderScreen(CurrentUser user);

    void EnsureCanCreateRevision(CurrentUser user);

    void EnsureCanEditRevision(CurrentUser user, RfqCase rfqCase);

    void EnsureCanConfirmRevision(CurrentUser user, RfqCase rfqCase);

    void EnsureCanDiscardRevision(CurrentUser user, RfqCase rfqCase);

    void EnsureCanPickUp(CurrentUser user, RfqCase rfqCase, bool confirmed);

    void EnsureCanRelease(CurrentUser user, RfqCase rfqCase);

    void EnsureCanAssignTrader(CurrentUser user, RfqCase rfqCase);

    void EnsureCanTakeOver(CurrentUser user, RfqCase rfqCase, bool confirmed);

    void EnsureCanQuote(CurrentUser user, QuoteAuthorizationState state);

    void EnsureCanConfirmQuote(CurrentUser user, RfqCase rfqCase);

    void EnsureCanPresent(CurrentUser user, RfqCase rfqCase);
    void EnsureCanWithdraw(CurrentUser user, RfqCase rfqCase);
    void EnsureCanCancelOrReopen(CurrentUser user, RfqCase rfqCase);

    void EnsureCanClose(CurrentUser user, RfqCase rfqCase);

    void EnsureCanCorrectOutcome(CurrentUser user, RfqCase rfqCase);

    void EnsureCanChangeContactOwner(CurrentUser user, RfqCase rfqCase);

    void EnsureCanUpdateSalesMemo(CurrentUser user);

    void EnsureCanUpdateTraderMemo(CurrentUser user);
}

public sealed record QuoteAuthorizationState(
    bool IsOpen,
    UserId AssignedTraderId,
    Ownership? Ownership,
    ActiveQuoteState? QuoteState);
