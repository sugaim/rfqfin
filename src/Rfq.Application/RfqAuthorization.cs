using Rfq.Domain;

namespace Rfq.Application;

public sealed record QuoteAuthorizationState(
    bool IsOpen,
    UserId AssignedTraderId,
    bool Owned,
    string? QuoteStatus);

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
}

public sealed class RfqAuthorization : IRfqAuthorization
{
    public void EnsureCanViewTraderScreen(CurrentUser user) =>
        EnsureRole(user, UserRole.Trader);

    public void EnsureCanCreateRevision(CurrentUser user) => EnsureRole(user, UserRole.Sales);

    public void EnsureCanEditRevision(CurrentUser user, RfqCase rfqCase) =>
        EnsureContactOwner(user, rfqCase);

    public void EnsureCanConfirmRevision(CurrentUser user, RfqCase rfqCase) =>
        EnsureContactOwner(user, rfqCase);

    public void EnsureCanDiscardRevision(CurrentUser user, RfqCase rfqCase) =>
        EnsureContactOwner(user, rfqCase);

    public void EnsureCanPickUp(CurrentUser user, RfqCase rfqCase, bool confirmed)
    {
        EnsureRole(user, UserRole.Trader);
        EnsureOpen(rfqCase);
        if (rfqCase.Owned)
        {
            throw new InvalidOperationException("Owned RFQs cannot be picked up; use Take Over.");
        }

        if (rfqCase.AssignedTraderId != user.UserId && !confirmed)
        {
            throw new ArgumentException(
                "Confirmation is required to pick up an RFQ assigned to another Trader.",
                nameof(confirmed));
        }
    }

    public void EnsureCanRelease(CurrentUser user, RfqCase rfqCase)
    {
        EnsureRole(user, UserRole.Trader);
        EnsureOpen(rfqCase);
        if (!rfqCase.Owned)
        {
            throw new InvalidOperationException("The RFQ is not owned.");
        }

        if (rfqCase.AssignedTraderId != user.UserId)
        {
            throw new UnauthorizedAccessException("Only the owning Trader can release the RFQ.");
        }
    }

    public void EnsureCanAssignTrader(CurrentUser user, RfqCase rfqCase)
    {
        EnsureRole(user, UserRole.Trader);
        EnsureOpen(rfqCase);
        if (rfqCase.Owned)
        {
            throw new InvalidOperationException("Owned RFQs cannot be assigned.");
        }
    }

    public void EnsureCanTakeOver(CurrentUser user, RfqCase rfqCase, bool confirmed)
    {
        EnsureRole(user, UserRole.Trader);
        EnsureOpen(rfqCase);
        if (!rfqCase.Owned)
        {
            throw new InvalidOperationException("Unowned RFQs do not require Take Over.");
        }

        if (rfqCase.AssignedTraderId == user.UserId)
        {
            throw new InvalidOperationException("The RFQ is already owned by this Trader.");
        }

        if (!confirmed)
        {
            throw new ArgumentException(
                "Strong confirmation is required to take over an owned RFQ.",
                nameof(confirmed));
        }
    }

    public void EnsureCanQuote(CurrentUser user, QuoteAuthorizationState state)
    {
        EnsureRole(user, UserRole.Trader);
        if (!state.IsOpen)
        {
            throw new InvalidOperationException("Quotes can only be edited for an Open RFQ.");
        }

        if (!state.Owned || state.AssignedTraderId != user.UserId)
        {
            throw new UnauthorizedAccessException(
                "Only the owning Trader can edit the WorkingQuote.");
        }

        if (!string.Equals(state.QuoteStatus, "Requested", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "WorkingQuote editing requires QuoteStatus Requested.");
        }
    }

    public void EnsureCanConfirmQuote(CurrentUser user, RfqCase rfqCase)
    {
        EnsureRole(user, UserRole.Trader);
        EnsureOpen(rfqCase);
        if (!rfqCase.Owned || rfqCase.AssignedTraderId != user.UserId)
        {
            throw new UnauthorizedAccessException(
                "Only the owning Trader can Confirm the WorkingQuote.");
        }

        if (rfqCase.QuoteStatus != Domain.QuoteStatus.Requested)
        {
            throw new InvalidOperationException(
                "Quote Confirm requires QuoteStatus Requested.");
        }
    }

    public void EnsureCanPresent(CurrentUser user, RfqCase rfqCase)
    {
        EnsureOpen(rfqCase);
        if (rfqCase.ContactOwnerId != user.UserId)
        {
            throw new UnauthorizedAccessException(
                "Only the current Contact Owner can Present or Unpresent the RFQ.");
        }
    }

    private static void EnsureContactOwner(CurrentUser user, RfqCase rfqCase)
    {
        EnsureRole(user, UserRole.Sales);
        if (rfqCase.ContactOwnerId != user.UserId)
        {
            throw new UnauthorizedAccessException(
                "Only the current Contact Owner can change the Revision.");
        }
    }

    private static void EnsureRole(CurrentUser user, UserRole role)
    {
        if (!user.Roles.Contains(role))
        {
            throw new UnauthorizedAccessException($"The {role} role is required.");
        }
    }

    private static void EnsureOpen(RfqCase rfqCase)
    {
        if (rfqCase.Lifecycle is not OpenRfq)
        {
            throw new InvalidOperationException("Ownership can only change for an Open RFQ.");
        }
    }
}
