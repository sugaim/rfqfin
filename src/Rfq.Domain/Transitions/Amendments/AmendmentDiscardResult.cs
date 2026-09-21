namespace Rfq.Domain;

public sealed record AmendmentDiscardResult(RfqCase Rfq, RfqRevision DiscardedRevision);
