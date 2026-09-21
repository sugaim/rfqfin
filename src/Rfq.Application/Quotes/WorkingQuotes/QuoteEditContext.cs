using Rfq.Domain;

namespace Rfq.Application;

public sealed record QuoteEditContext(
    CaseId CaseId,
    RevisionId RevisionId,
    SecurityId SecurityId,
    DateOnly SettlementDate,
    StateVersion Version,
    UserId AssignedTraderId,
    Ownership Ownership,
    ActiveQuoteState QuoteState,
    WorkingQuote WorkingQuote);
