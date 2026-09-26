# Case History, Restore, Reopen, Trace, and Replayable Operations

## Status

Active retained rationale for the canonical RfqCaseRevision / Restore / Reopen / TraceRecord foundation.

The canonical authority is `../../../domain.md`. This note preserves the reasoning bridge into historical correction without duplicating the full canonical model.

The earlier abstract correction-history model is not the current semantic starting point.

## 1. Layered model

The current model separates:

    RfqCase
      = one complete valid business state

    RfqCaseRevision
      = one immutable operational occurrence of that state

    RfqCaseHistory
      = retained operational chronology for one CaseId

    TraceRecord
      = one durable record materializing a CaseActivityDigest

    CaseActivityDigest
      = compressed, business-semantic activity representation
        resolved from the effective operational path

RfqCaseHistory is operational chronology. CaseActivityDigest is not an event-store copy and intentionally omits some operational detail.

TraceRecord may outlive RfqCaseHistory.

## 2. First-class ordinary Case operations

Ordinary business operations are first-class Domain Objects.

Conceptually:

    Apply :
        RfqCase x CaseOperation
        -> RfqCase

The operation set is:

    CaseOperation
    = Open(OpenOperation)
    | Terminal(TerminalOperation)
    | Reopen(ReopenOperation)

with:

    OpenOperation     : Open -> Open
    TerminalOperation : Open -> Terminal
    ReopenOperation   : Terminal -> Open

The same fully resolved CaseOperation value is used for:

- live Domain processing;
- RfqCaseRevision.Applied provenance;
- future retained-history replay/correction.

Application request models are separate. Application resolves actor/time/date values, generated Case-local IDs, external context, and other exogenous inputs before constructing the Domain Operation.

Values deterministically available from the source RfqCase are not duplicated merely for replay convenience.

## 3. Revision transition provenance

Canonical transition provenance is:

    RfqCaseTransition
    = Published(DraftId)
    | Applied(CaseOperation)
    | RestoredFrom(TargetVersion)

Reopen is genuine positive-flow business activity and therefore creates:

    Applied(Reopen(...))

Operational Restore is different: it selects an earlier valid Open revision and therefore uses RestoredFrom.

One accepted CaseOperation creates exactly one next revision.

CaseVersionNumber provides the version-sequence operations used by revision chronology:

    CaseVersionNumber
    - Value
    - New()
    - Next()
    - Prev() : CaseVersionNumber?

New() creates the initial Case version. Prev() has no value at that initial version.

## 4. Operational Restore

Restore means the currently effective operational path was mistaken.

Given:

    v10 = earlier Open revision
    ...
    v20 = current revision

the revision-level Domain operation is:

    RestoreOperation
    - TargetVersion : CaseVersionNumber

with:

    operation.TargetVersion = v10.Version

ApplyRestore creates:

    v21
    - Case = v10.Case
    - Transition = RestoredFrom(v10.Version)

The abandoned chronology remains immutable.

Restore is not a CaseActivityDigest activity. It changes which operational path is current.

Restore produces one TraceRecord with Trigger=Restore whose Digest represents the abandoned path immediately before Restore. Digest.EndContext is the abandoned path endpoint, not the restored target.

Restore does not create another TraceRecord merely to restate the restored Open revision.

## 5. Genuine Reopen

Reopen means the terminal occurrence was correct and remains genuine business activity, but the same negotiation context later resumes.

The same CaseId continues. Whether resumed activity belongs to the same negotiation context is a business assertion by the operator/Application; it is not inferred from matching fields.

Reopen is one ordinary business operation:

    ReopenOperation
    - NewPricingEpisodeId
    - ReopenDate : BusinessEntityLocalDate
    - NewAssumedTradeDate : BusinessEntityLocalDate
    - ReopenedBy : ActorId
    - ReopenedAt : Timepoint

The source Terminal state determines the reopen provenance and resulting Open state; the caller does not choose a reopen variant.

Positive-flow applicability:

    Closed(Presented(Away))
        -> Reopen
        -> Negotiating.Pricing

    Closed(Unpresented)
        -> Reopen
        -> Inquiry.Pricing

    Cancelled(Withdrawn), PriorPresentation=None
        -> Reopen
        -> Inquiry.Pricing

    Cancelled(Withdrawn), PriorPresentation=Some
        -> Reopen
        -> Negotiating.Pricing

Closed(Hit) is not reopenable through ordinary positive flow.

Cancelled(CreatedInError) is not reopenable through ordinary positive flow.

Every Reopen creates a fresh PricingEpisode and never restores a FirmQuote.

## 6. Terminal pricing context

Terminal state retains a logically meaningful pricing context rather than a Reopen-specific cache:

    TerminalPricingContext
    - PricingEpisode
    - PriorPresentation?

    PriorPresentation
    - Presentation
    - AwayOutcome?

All terminal states carry TerminalPricingContext, including Hit.

It records the pricing lineage from which the Case terminated. Reopen merely consumes that already-meaningful terminal fact.

For cancellation, presentation context is preserved when one existed. Cancellation does not erase a prior customer Presentation.

## 7. Reopen pricing provenance

New Reopen Episodes use:

    ReopenedFromAway(
        PreviousPricingEpisodeId,
        PresentationAwayOutcome
    )

    ReopenedFromUnpresented(
        PreviousPricingEpisodeId,
        Feedback?
    )

    ReopenedFromCancellation(
        PreviousPricingEpisodeId,
        PriorPresentation?
    )

A Reopen operation explicitly supplies:

- NewPricingEpisodeId;
- ReopenDate;
- NewAssumedTradeDate;
- ReopenedBy;
- ReopenedAt.

The new Episode preserves current RfqTerms and prior QuoteOwner, sets PricingDate=ReopenDate, and uses the explicit NewAssumedTradeDate.

No chronology invariant requires ReopenDate / ReopenedAt to be after the prior terminal date/time.

Application/UI may provide a convenience default for NewAssumedTradeDate, but the Domain receives the resolved explicit value.

## 8. Cancellation reason boundary

The current minimal positive-flow taxonomy is:

    CancellationReason
    = Withdrawn
    | CreatedInError

Withdrawn means a genuine Case was stopped and is the only ordinary cancellation reason eligible for Reopen.

CreatedInError includes duplicate or mistaken creation cases. If that classification itself was wrong, correction/Restore semantics apply rather than ordinary Reopen.

Future business/statistical requirements may refine the non-reopenable taxonomy.

## 9. Canonical simplified Trace model

The simplified Trace semantics discussed after Session 08 have now been reconfirmed and incorporated into canonical `../../../domain.md`.

Trace is the public/analysis-oriented business representation of the effective Case path. It is intentionally not a copy of RfqCaseHistory.

The durable outer record currently remains:

    TraceRecord
    - RecordedBy
    - RecordedAt
    - RecordedBusinessDate
    - Trigger : TraceTrigger
    - Digest : CaseActivityDigest

    TraceTrigger
    = HitClose
    | AwayClose
    | UnpresentedClose
    | Cancelled
    | Reopened
    | Restore
    | HistoricalCorrection

Trigger describes why the Trace was materialized. It constrains the represented Digest in the forward direction, but activity shape does not uniquely determine Trigger. This is important for HistoricalCorrection.

The current trigger variants carry no payload. Additional correction/audit provenance is deferred.

## 10. Canonical Trace contexts

    CaseActivityDigest
    - CaseId
    - BusinessEntity
    - OpenDate
    - InitialContext : TraceCaseContext
    - EndContext : TraceCaseContext
    - Activities : TraceActivity[]

    TraceCaseContext
    - Terms
    - ContactOwnerId
    - QuoteOwnerId
    - PricingDate
    - AssumedTradeDate

InitialContext is publication context. EndContext is the endpoint context of the represented effective path.

For Trigger=Restore, EndContext is the abandoned path endpoint immediately before Restore, not the restored target.

## 11. Canonical Trace activities

The canonical activity set is now:

    TraceActivity
    = TermsAmended
    | Presented
    | Away
    | RepricingRequested
    | Hit
    | Closed
    | Cancelled
    | Reopened

Important semantics:

- TermsAmended carries the full post-change TraceTerms snapshot plus actor/time;
- Presented is self-contained and carries Terms, ContactOwnerId, QuoteOwnerId, PricingDate, AssumedTradeDate, Quote, and presentation actor/date/time;
- Presented.Quote.EffectiveValidUntil is resolved from the represented effective path and may incorporate later ExtendValidity;
- Away carries AwayDate, RecordedBy, RecordedAt, and optional Feedback;
- RepricingRequested represents ordinary RequestRepricing and carries RejectedQuoteValue, optional Feedback, RequestedBy, and RequestedAt;
- Hit carries HitDate and HitAt;
- Closed carries CaseClose plus optional typed CloseFeedback; current CloseFeedback is UnpresentedCloseFeedback only;
- Cancelled reuses Cancellation;
- Reopened carries ReopenDate, ReopenedBy, and ReopenedAt.

Restore is not a TraceActivity.

Hit/Away association is chronological rather than identity-based:

- every Hit/Away requires at least one preceding Presented;
- it applies to the latest preceding Presented;
- one Presented may have at most one Hit/Away outcome.

The public Trace therefore exposes no PresentationId.

Every Reopened follows a reopenable Closed or Cancelled terminal occurrence.

## 12. Canonical operation-to-activity mapping

Trace activity selection is mapping-based rather than split by a FirstPresentation compression boundary.

| Accepted operation | Trace activities |
| --- | --- |
| CommitQuote | none |
| ReplaceFirmQuote | none |
| InvalidateQuote | none |
| RequestRepricing | RepricingRequested |
| ChangeRfqTerms | TermsAmended |
| ChangeQuoteOwner | none |
| RollPricingDate | none |
| ChangeAssumedTradeDate | none |
| PresentQuote | Presented |
| ExtendValidity | none directly; updates Presented.Quote.EffectiveValidUntil |
| RequestRepricingOnAway | Away |
| ChangeContactOwner | none |
| CloseCase(Hit) | Hit, Closed |
| CloseCase(Away.RecordNow) | Away, Closed |
| CloseCase(Away.AlreadyRecorded) | Closed |
| CloseCase(Unpresented) | Closed |
| Cancel | Cancelled |
| Reopen | Reopened |

Operational Restore creates no TraceActivity.

There is no special Trace rule for pre-Presentation chronology. ChangeRfqTerms happens pre-Presentation because of RfqCase applicability, not because Trace treats that phase specially.

## 13. History resolution and retention

Trace construction resolves Case-local IDs to durable business values while RfqCaseHistory is available. Internal materialization may still use those IDs as temporary lookup keys.

Presented requires source-state resolution for Terms/owners/pricing/Quote and may require forward path resolution for EffectiveValidUntil.

RepricingRequested resolves RejectedQuoteValue from the source PendingPresentation FirmQuote.

TermsAmended may use the resulting revision's RfqTerms as the full post-change snapshot.

Reopened TraceRecord construction remains independently materialized from retained operational History rather than copied from an earlier TraceRecord.

There is no fallback from missing History to copying an older TraceRecord. Retention must therefore preserve operational History while a supported history-dependent operation still needs it.

## 14. Revision + Trace API

The canonical revision-level API remains:

    RevisionTraceResult
    - Revision
    - Trace : TraceRecord

    ApplyTerminal(
        history : RfqCaseHistory,
        operation : TerminalOperation,
        traceContext : TraceContext
    ) -> RevisionTraceResult

    ApplyReopen(
        history : RfqCaseHistory,
        operation : ReopenOperation,
        traceContext : TraceContext
    ) -> RevisionTraceResult

    ApplyRestore(
        history : RfqCaseHistory,
        operation : RestoreOperation,
        traceContext : TraceContext
    ) -> RevisionTraceResult

    TraceContext
    - RecordedBy : ActorId
    - RecordedAt : Timepoint
    - RecordedBusinessDate : BusinessEntityLocalDate

RfqCaseHistory remains the single source of current revision/current Case and retained chronology. Do not reintroduce separately supplied current Case/revision values.

Revision + TraceRecord produced by one Domain operation should be persisted atomically.

## 15. Deferred audit/business facts

A multi-jurisdiction regulatory review identified several business facts worth later consideration, but they are intentionally not part of the current canonical Trace update:

- initial customer instruction originator / ReceivedBy / ReceivedAt;
- receipt metadata for customer-originated amendment or withdrawal;
- the desk-side actor who received/executed a Hit, if Hit is intended to represent trade execution rather than only customer outcome;
- richer Trigger/correction provenance such as actor/time/reason.

Communication infrastructure, message archives, voice recording, venue routing, and similar infrastructure records remain outside this Trace model.

## 16. Next unresolved design questions

The Trace semantic model itself is now canonical. The remaining active boundary is TraceRevision.

Next questions:

1. TraceRevision / TraceVersionNumber identity and sequence semantics;
2. how Trace-producing Domain operations obtain the latest/previous Trace revision without reintroducing inconsistent duplicated inputs;
3. the final RevisionTraceResult shape after TraceRecord becomes TraceRevision;
4. HistoricalCorrection production API while RfqCaseHistory remains immutable;
5. Trigger/correction provenance payloads, including the deferred audit review;
6. concrete historical-correction construction strategy and regression cases.

Do not treat the earlier working Trace-simplification section as pending anymore; those semantics are now canonical.

## 17. Bridge to historical correction

The concrete retained foundation is now:

    initial Published revision
      + Applied(CaseOperation)
      + RestoredFrom provenance
      + durable TraceRecord milestones

Reopen is no longer an unresolved prerequisite.

The next historical-correction question is replay source/path selection when RfqCaseHistory contains:

- physical v1..vn chronology;
- RestoredFrom edges;
- paths that were once effective and later abandoned;
- terminal/Reopen milestone TraceRecords.

Do not assume that the physical revision sequence is one linear replay program.

The next design unit must determine:

- what semantic ancestry/path is selected when correcting a historical business occurrence;
- how RestoredFrom participates in replay;
- how current effective-state reconstruction differs from reconstruction of an abandoned historical path;
- how correction targets a path that is no longer current but was once effective.

Use the concrete correction catalog rather than restarting from the old Supports(H_rep, H_true) abstraction.

## 18. Canonical incorporation

The decisions in this note are incorporated into `../../../domain.md`.

Design Sessions 05 and 06 remain historical rationale for the operational-history and first-class-operation foundations. Session 07 records the Reopen and CaseActivityDigest refinement.

Historical correction replay source/path selection remains the active next design problem.
