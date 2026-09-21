namespace Rfq.Domain;

public static class RfqOwnershipTransitions
{
    public static RfqCase PickUp(RfqCase rfq, UserId traderId, StateVersion expectedVersion)
    {
        var open = Open(rfq, expectedVersion);
        if (open.Ownership is Owned)
            throw new DomainRuleViolationException("Owned RFQs cannot be picked up.");
        return rfq.Next(assignedTraderId: traderId, lifecycle: WithOwnership(open, new Owned()));
    }

    public static RfqCase Release(RfqCase rfq, UserId traderId, StateVersion expectedVersion)
    {
        var open = Open(rfq, expectedVersion);
        if (open.Ownership is not Owned || rfq.AssignedTraderId != traderId)
            throw new DomainRuleViolationException("Only the owning Trader can release the RFQ.");
        return rfq.Next(lifecycle: WithOwnership(open, new Unowned()));
    }

    public static RfqCase Assign(RfqCase rfq, UserId traderId, StateVersion expectedVersion)
    {
        var open = Open(rfq, expectedVersion);
        if (open.Ownership is Owned)
            throw new DomainRuleViolationException("Owned RFQs cannot be assigned.");
        return rfq.Next(assignedTraderId: traderId, lifecycle: WithOwnership(open, new Unowned()));
    }

    public static RfqCase TakeOver(RfqCase rfq, UserId traderId, StateVersion expectedVersion)
    {
        var open = Open(rfq, expectedVersion);
        if (open.Ownership is not Owned)
            throw new DomainRuleViolationException("Unowned RFQs do not require Take Over.");
        if (rfq.AssignedTraderId == traderId)
            throw new DomainRuleViolationException("The RFQ is already owned by this Trader.");
        return rfq.Next(assignedTraderId: traderId, lifecycle: WithOwnership(open, new Owned()));
    }

    private static OpenRfq Open(RfqCase rfq, StateVersion expected)
    {
        rfq.EnsureVersion(expected);
        return rfq.Lifecycle as OpenRfq
            ?? throw new DomainRuleViolationException("Ownership can only change for an Open RFQ.");
    }

    private static OpenRfq WithOwnership(OpenRfq open, Ownership ownership) => open switch
    {
        ActiveRfq active => new ActiveRfq(active.CurrentRevisionId, ownership, active.QuoteState),
        PresentedRfq presented => new PresentedRfq(presented.CurrentRevisionId, ownership, presented.QuoteId),
        _ => throw new DomainInvariantException("Unknown Open RFQ subtype.")
    };
}
