namespace Rfq.Domain;

public sealed class CancelledRfq(RevisionId currentRevisionId) : RfqLifecycle(currentRevisionId);
