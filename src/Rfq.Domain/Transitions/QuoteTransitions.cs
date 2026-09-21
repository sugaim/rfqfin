namespace Rfq.Domain;

public static class QuoteTransitions
{
    public static QuoteConfirmationResult Confirm(
        RfqCase rfq, WorkingQuote workingQuote, QuoteId quoteId,
        QuoteConfirmation confirmation)
    {
        if (rfq.Lifecycle is not ActiveRfq
            { QuoteState: QuoteRequested requested } active)
            throw new DomainRuleViolationException(
                "Quote Confirm requires an Active requested quote.");
        if (workingQuote.RevisionId != rfq.CurrentRevision.RevisionId)
            throw new DomainRuleViolationException(
                "The WorkingQuote does not belong to the current Revision.");
        var settlementDate = rfq.CurrentRevision.SettlementDate
            ?? throw new DomainInvariantException("Current Revision is missing SettlementDate.");

        CalculatedQuotePayload? calculated = null;
        ManualQuotePayload? manual = null;
        if (workingQuote.Mode == WorkingQuoteMode.Calculated)
        {
            calculated = workingQuote.Calculated
                ?? throw new DomainRuleViolationException(
                    "Calculated mode requires a successful calculated payload before Confirm.");
        }
        else
        {
            manual = workingQuote.Manual;
            if (manual?.Price is null || manual.FinalSimpleYield is null)
                throw new DomainRuleViolationException(
                    "Manual mode requires both Price and Final Simple Yield before Confirm.");
        }

        var (minutes, expiresAt) = confirmation.Expiry switch
        {
            QuoteExpiry.None => ((int?)null, (DateTimeOffset?)null),
            QuoteExpiry.After after => (
                checked((int)after.Duration.TotalMinutes),
                confirmation.ConfirmedAt.Add(after.Duration)),
            _ => throw new DomainInvariantException("Unknown quote expiry policy.")
        };
        var quote = new ConfirmedQuote(
            quoteId, workingQuote.RevisionId, rfq.SecurityId, settlementDate,
            confirmation.ConfirmedBy, confirmation.ConfirmedAt, workingQuote.Mode,
            calculated, manual, minutes, expiresAt, requested.Reason);
        var next = rfq.Next(lifecycle: new ActiveRfq(
            active.CurrentRevisionId, active.Ownership, new QuoteConfirmed(quoteId)));
        return new QuoteConfirmationResult(next, quote);
    }

    public static RfqCase Withdraw(RfqCase rfq, StateVersion expectedVersion)
    {
        rfq.EnsureVersion(expectedVersion);
        if (rfq.Lifecycle is PresentedRfq)
            throw new DomainRuleViolationException("A Presented quote cannot be withdrawn.");
        if (rfq.Lifecycle is not ActiveRfq
            { QuoteState: QuoteConfirmed } active)
            throw new DomainRuleViolationException("Only a confirmed quote can be withdrawn.");
        return rfq.Next(lifecycle: new ActiveRfq(
            active.CurrentRevisionId, active.Ownership,
            new QuoteRequested(QuoteRequestReason.Withdrawn)));
    }

    public static RfqCase Expire(
        RfqCase rfq, QuoteId quoteId, StateVersion expectedVersion)
    {
        rfq.EnsureVersion(expectedVersion);
        var open = rfq.Lifecycle as OpenRfq
            ?? throw new DomainRuleViolationException("Only an Open quote can expire.");
        var current = rfq.CurrentQuoteId;
        if (current != quoteId)
            throw new DomainRuleViolationException("The quote is no longer current.");
        return rfq.Next(lifecycle: new ActiveRfq(
            open.CurrentRevisionId, open.Ownership,
            new QuoteRequested(QuoteRequestReason.Expired)));
    }
}
