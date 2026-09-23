using Rfq.Domain;

namespace Rfq.Application;

public sealed record ExpiredQuoteCandidate(
    CaseId CaseId,
    QuoteId QuoteId,
    StateVersion CurrentVersion);
