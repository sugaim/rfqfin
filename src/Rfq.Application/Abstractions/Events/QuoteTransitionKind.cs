using Rfq.Domain;

namespace Rfq.Application;

public enum QuoteTransitionKind
{
    Confirmed,
    Presented,
    Unpresented,
    Withdrawn,
    Expired,
}
