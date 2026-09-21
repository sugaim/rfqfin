namespace Rfq.Domain;

public sealed record AmendmentConfirmResult(
    RfqCase Rfq, RfqRevision SupersededRevision, RfqRevision ConfirmedRevision);
