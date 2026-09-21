namespace Rfq.Domain;

public sealed class DraftRfq(RevisionId currentRevisionId) : RfqLifecycle(currentRevisionId);
