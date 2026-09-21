namespace Rfq.Domain;

public abstract record RfqLifecycle;

public sealed record DraftRfq(RevisionId CurrentRevisionId) : RfqLifecycle;

public sealed record CancelledRfq(
    RevisionId CurrentRevisionId,
    UserId ContactOwnerId,
    UserId AssignedTraderId) : RfqLifecycle;

public sealed record ClosedRfq : RfqLifecycle
{
    public ClosedRfq(
        RevisionId currentRevisionId,
        UserId contactOwnerId,
        UserId assignedTraderId,
        QuoteId closedQuoteId,
        RfqStatus outcome)
    {
        if (outcome is not RfqStatus.Hit and not RfqStatus.Away)
        {
            throw new ArgumentException("A Closed RFQ outcome must be Hit or Away.", nameof(outcome));
        }

        CurrentRevisionId = currentRevisionId;
        ContactOwnerId = contactOwnerId;
        AssignedTraderId = assignedTraderId;
        ClosedQuoteId = closedQuoteId;
        Outcome = outcome;
    }

    public RevisionId CurrentRevisionId { get; }

    public UserId ContactOwnerId { get; }

    public UserId AssignedTraderId { get; }

    public QuoteId ClosedQuoteId { get; }

    public RfqStatus Outcome { get; }
}

public sealed record OpenRfq : RfqLifecycle
{
    public OpenRfq(
        RevisionId currentRevisionId,
        UserId contactOwnerId,
        UserId assignedTraderId,
        QuoteStatus quoteStatus,
        QuoteRequestReason? quoteRequestReason,
        bool owned,
        OpenRfqStatus status = OpenRfqStatus.Active,
        QuoteId? currentQuoteId = null)
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

        if (quoteStatus == QuoteStatus.Requested && currentQuoteId is not null)
        {
            throw new ArgumentException(
                "A Requested RFQ cannot have a current ConfirmedQuote.",
                nameof(currentQuoteId));
        }

        if (quoteStatus == QuoteStatus.Quoted && currentQuoteId is null)
        {
            throw new ArgumentException(
                "A Quoted RFQ requires a current ConfirmedQuote.",
                nameof(currentQuoteId));
        }

        if (status == OpenRfqStatus.Presented && quoteStatus != QuoteStatus.Quoted)
        {
            throw new ArgumentException(
                "Presented requires QuoteStatus Quoted.",
                nameof(status));
        }

        CurrentRevisionId = currentRevisionId;
        ContactOwnerId = contactOwnerId;
        AssignedTraderId = assignedTraderId;
        QuoteStatus = quoteStatus;
        QuoteRequestReason = quoteRequestReason;
        Owned = owned;
        Status = status;
        CurrentQuoteId = currentQuoteId;
    }

    public RevisionId CurrentRevisionId { get; }

    public UserId ContactOwnerId { get; }

    public UserId AssignedTraderId { get; }

    public QuoteStatus QuoteStatus { get; }

    public QuoteRequestReason? QuoteRequestReason { get; }

    public bool Owned { get; }

    public OpenRfqStatus Status { get; }

    public QuoteId? CurrentQuoteId { get; }
}

public enum OpenRfqStatus
{
    Active,
    Presented,
}

public enum RfqLifecycleKind
{
    Draft,
    Open,
    Cancelled,
    Closed,
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
