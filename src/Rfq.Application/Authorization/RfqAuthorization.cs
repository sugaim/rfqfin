using Rfq.Domain;

namespace Rfq.Application;

public sealed class RfqAuthorization : IRfqAuthorization
{
    public void EnsureCanViewTraderScreen(CurrentUser user) =>
        EnsureRole(user, UserRole.Trader);

    public void EnsureCanCreateRevision(CurrentUser user) => EnsureSalesOrTrader(user);

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
        if (rfqCase.Ownership is Owned)
        {
            throw new DomainRuleViolationException("Owned RFQs cannot be picked up; use Take Over.");
        }

        if (rfqCase.AssignedTraderId != user.UserId && !confirmed)
        {
            throw new RfqRequestValidationException(
                "Confirmation is required to pick up an RFQ assigned to another Trader.");
        }
    }

    public void EnsureCanRelease(CurrentUser user, RfqCase rfqCase)
    {
        EnsureRole(user, UserRole.Trader);
        EnsureOpen(rfqCase);
        if (rfqCase.Ownership is not Owned)
        {
            throw new DomainRuleViolationException("The RFQ is not owned.");
        }

        if (rfqCase.AssignedTraderId != user.UserId)
        {
            throw new RfqForbiddenException("Only the owning Trader can release the RFQ.");
        }
    }

    public void EnsureCanAssignTrader(CurrentUser user, RfqCase rfqCase)
    {
        EnsureRole(user, UserRole.Trader);
        EnsureOpen(rfqCase);
        if (rfqCase.Ownership is Owned)
        {
            throw new DomainRuleViolationException("Owned RFQs cannot be assigned.");
        }
    }

    public void EnsureCanTakeOver(CurrentUser user, RfqCase rfqCase, bool confirmed)
    {
        EnsureRole(user, UserRole.Trader);
        EnsureOpen(rfqCase);
        if (rfqCase.Ownership is not Owned)
        {
            throw new DomainRuleViolationException("Unowned RFQs do not require Take Over.");
        }

        if (rfqCase.AssignedTraderId == user.UserId)
        {
            throw new DomainRuleViolationException("The RFQ is already owned by this Trader.");
        }

        if (!confirmed)
        {
            throw new RfqRequestValidationException(
                "Strong confirmation is required to take over an owned RFQ.");
        }
    }

    public void EnsureCanQuote(CurrentUser user, QuoteAuthorizationState state)
    {
        EnsureRole(user, UserRole.Trader);
        if (!state.IsOpen)
        {
            throw new DomainRuleViolationException("Quotes can only be edited for an Open RFQ.");
        }

        if (state.Ownership is not Owned || state.AssignedTraderId != user.UserId)
        {
            throw new RfqForbiddenException(
                "Only the owning Trader can edit the WorkingQuote.");
        }

        if (state.QuoteState is not QuoteRequested)
        {
            throw new DomainRuleViolationException(
                "WorkingQuote editing requires QuoteStatus Requested.");
        }
    }

    public void EnsureCanConfirmQuote(CurrentUser user, RfqCase rfqCase)
    {
        EnsureRole(user, UserRole.Trader);
        EnsureOpen(rfqCase);
        if (rfqCase.Ownership is not Owned || rfqCase.AssignedTraderId != user.UserId)
        {
            throw new RfqForbiddenException(
                "Only the owning Trader can Confirm the WorkingQuote.");
        }

        if (rfqCase.QuoteStatus != Domain.QuoteStatus.Requested)
        {
            throw new DomainRuleViolationException(
                "Quote Confirm requires QuoteStatus Requested.");
        }
    }

    public void EnsureCanPresent(CurrentUser user, RfqCase rfqCase)
    {
        EnsureOpen(rfqCase);
        if (rfqCase.ContactOwnerId != user.UserId)
        {
            throw new RfqForbiddenException(
                "Only the current Contact Owner can Present or Unpresent the RFQ.");
        }
    }

    public void EnsureCanWithdraw(CurrentUser user, RfqCase rfqCase)
    {
        EnsureRole(user, UserRole.Trader);
        EnsureOpen(rfqCase);
        if (rfqCase.Ownership is not Owned || rfqCase.AssignedTraderId != user.UserId)
        {
            throw new RfqForbiddenException(
                "Only the owning Trader can withdraw a quote.");
        }
    }

    public void EnsureCanCancelOrReopen(CurrentUser user, RfqCase rfqCase) =>
        EnsureContactOwnerIdentity(user, rfqCase, "change the lifecycle of");

    public void EnsureCanClose(CurrentUser user, RfqCase rfqCase)
    {
        EnsureContactOwnerIdentity(user, rfqCase, "Close");
        EnsureOpen(rfqCase);
    }

    public void EnsureCanCorrectOutcome(CurrentUser user, RfqCase rfqCase)
    {
        EnsureContactOwnerIdentity(user, rfqCase, "correct the outcome of");
        if (rfqCase.Lifecycle is not ClosedRfq)
        {
            throw new DomainRuleViolationException("Only a Closed RFQ outcome can be corrected.");
        }
    }

    public void EnsureCanChangeContactOwner(CurrentUser user, RfqCase rfqCase)
    {
        EnsureContactOwnerIdentity(user, rfqCase, "change the Contact Owner of");
        EnsureOpen(rfqCase);
    }

    public void EnsureCanUpdateSalesMemo(CurrentUser user) =>
        EnsureRole(user, UserRole.Sales);

    public void EnsureCanUpdateTraderMemo(CurrentUser user) =>
        EnsureRole(user, UserRole.Trader);

    private static void EnsureContactOwner(CurrentUser user, RfqCase rfqCase)
    {
        EnsureSalesOrTrader(user);
        if (rfqCase.ContactOwnerId != user.UserId)
        {
            throw new RfqForbiddenException(
                "Only the current Contact Owner can change the Revision.");
        }
    }

    private static void EnsureContactOwnerIdentity(
        CurrentUser user,
        RfqCase rfqCase,
        string operation)
    {
        if (rfqCase.ContactOwnerId != user.UserId)
        {
            throw new RfqForbiddenException(
                $"Only the current Contact Owner can {operation} the RFQ.");
        }
    }

    private static void EnsureRole(CurrentUser user, UserRole role)
    {
        if (!user.Roles.Contains(role))
        {
            throw new RfqForbiddenException($"The {role} role is required.");
        }
    }

    private static void EnsureSalesOrTrader(CurrentUser user)
    {
        if (!user.Roles.Contains(UserRole.Sales)
            && !user.Roles.Contains(UserRole.Trader))
        {
            throw new RfqForbiddenException("The Sales or Trader role is required.");
        }
    }

    private static void EnsureOpen(RfqCase rfqCase)
    {
        if (rfqCase.Lifecycle is not OpenRfq)
        {
            throw new DomainRuleViolationException("The operation requires an Open RFQ.");
        }
    }
}
