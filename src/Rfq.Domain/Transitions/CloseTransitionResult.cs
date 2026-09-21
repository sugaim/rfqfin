namespace Rfq.Domain;

public sealed record CloseTransitionResult(RfqCase Rfq, RfqRevision? DiscardedRevision);
