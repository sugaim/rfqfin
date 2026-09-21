using Rfq.Domain;

namespace Rfq.Application;

public sealed record ExpiredQuoteCandidate(long CaseId, Guid QuoteId, long CurrentVersion);
