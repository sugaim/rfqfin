namespace Rfq.Domain;

public static class RfqResponsibilityTransitions
{
    public static RfqCase ChangeContactOwner(
        RfqCase rfq, UserId contactOwnerId, StateVersion expectedVersion)
    {
        rfq.EnsureVersion(expectedVersion);
        if (rfq.ContactOwnerId == contactOwnerId)
            throw new DomainRuleViolationException("The selected user is already the Contact Owner.");
        return rfq.Next(contactOwnerId: contactOwnerId);
    }
}
