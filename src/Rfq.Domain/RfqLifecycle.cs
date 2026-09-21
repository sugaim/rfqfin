namespace Rfq.Domain;

public abstract record RfqLifecycle;

public sealed record DraftRfq(RevisionId CurrentRevisionId) : RfqLifecycle;

public sealed record OpenRfq : RfqLifecycle
{
    public OpenRfq(
        RevisionId currentRevisionId,
        UserId contactOwnerId,
        UserId assignedTraderId,
        QuoteStatus quoteStatus,
        QuoteRequestReason? quoteRequestReason,
        bool owned)
    {
        if (quoteStatus == QuoteStatus.Requested && quoteRequestReason is null)
        {
            throw new ArgumentException(
                "QuoteRequestReason is required when QuoteStatus is Requested.",
                nameof(quoteRequestReason));
        }

        if (quoteStatus == QuoteStatus.Quoted && quoteRequestReason is not null)
        {
            throw new ArgumentException(
                "QuoteRequestReason must be absent when QuoteStatus is Quoted.",
                nameof(quoteRequestReason));
        }

        CurrentRevisionId = currentRevisionId;
        ContactOwnerId = contactOwnerId;
        AssignedTraderId = assignedTraderId;
        QuoteStatus = quoteStatus;
        QuoteRequestReason = quoteRequestReason;
        Owned = owned;
    }

    public RevisionId CurrentRevisionId { get; }

    public UserId ContactOwnerId { get; }

    public UserId AssignedTraderId { get; }

    public QuoteStatus QuoteStatus { get; }

    public QuoteRequestReason? QuoteRequestReason { get; }

    public bool Owned { get; }
}

public enum RfqLifecycleKind
{
    Draft,
    Open,
}

public enum RfqStatus
{
    Draft,
    Active,
    Presented,
    Cancelled,
    Hit,
    Away,
}

public enum RevisionStatus
{
    Draft,
    Confirmed,
    Superseded,
    Discarded,
}

public enum QuoteStatus
{
    Requested,
    Quoted,
}

public enum QuoteRequestReason
{
    Initial,
    Revised,
    Reopened,
    Expired,
    Withdrawn,
}
