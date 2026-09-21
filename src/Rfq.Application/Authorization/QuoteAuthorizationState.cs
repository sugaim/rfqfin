using Rfq.Domain;

namespace Rfq.Application;

public sealed record QuoteAuthorizationState(
    bool IsOpen,
    UserId AssignedTraderId,
    Ownership? Ownership,
    ActiveQuoteState? QuoteState);
