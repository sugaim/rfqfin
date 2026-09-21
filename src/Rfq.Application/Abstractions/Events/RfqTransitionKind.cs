using Rfq.Domain;

namespace Rfq.Application;

public enum RfqTransitionKind
{
    ClosedHit,
    ClosedAway,
    OutcomeCorrected,
    ContactOwnerChanged,
    RevisionConfirmed,
    Cancelled,
    Reopened,
    PickedUp,
    Released,
    AssignedTraderChanged,
    TakenOver,
}
