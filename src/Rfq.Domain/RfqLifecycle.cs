namespace Rfq.Domain;

public abstract record RfqLifecycle;

public sealed record DraftRfq(RevisionId CurrentRevisionId) : RfqLifecycle;

public enum RfqLifecycleKind
{
    Draft,
}

public enum RfqStatus
{
    Draft,
}

public enum RevisionStatus
{
    Draft,
}
