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

## 4. Operational Restore

Restore means the currently effective operational path was mistaken.

Given:

    v10 = earlier Open revision
    ...
    v20 = current revision

Restore(v10) creates:

    v21
    - Case = v10.Case
    - Transition = RestoredFrom(v10.Version)

The abandoned chronology remains immutable.

Restore is not a CaseActivityDigest activity. It changes which operational path is current.

Restore produces one:

    TraceRecord
    Kind = RestoreSnapshot

whose Digest represents the abandoned path immediately before Restore. Digest.EndContext is the abandoned path endpoint, not the restored target.

Restore does not create another TraceRecord merely to restate the restored Open revision.

## 5. Genuine Reopen

Reopen means the terminal occurrence was correct and remains genuine business activity, but the same negotiation context later resumes.

The same CaseId continues. Whether resumed activity belongs to the same negotiation context is a business assertion by the operator/Application; it is not inferred from matching fields.

Normal Reopen variants are:

    ReopenOperation
    = ReopenAway
    | ReopenUnpresented
    | ReopenCancellation

Positive-flow applicability:

    Closed(Presented(Away))
        -> ReopenAway
        -> Negotiating.Pricing

    Closed(Unpresented)
        -> ReopenUnpresented
        -> Inquiry.Pricing

    Cancelled(Withdrawn), PriorPresentation=None
        -> ReopenCancellation
        -> Inquiry.Pricing

    Cancelled(Withdrawn), PriorPresentation=Some
        -> ReopenCancellation
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

## 9. TraceRecord and CaseActivityDigest

TraceRecord is now centered on business activities rather than an outer Open/Terminal trace-state hierarchy.

    TraceRecord
    - RecordedBy
    - RecordedAt
    - RecordedBusinessDate
    - Kind
    - Digest : CaseActivityDigest

    TraceRecordKind
    = HitClose
    | AwayClose
    | UnpresentedClose
    | Cancelled
    | Reopened
    | RestoreSnapshot

    CaseActivityDigest
    - CaseId
    - BusinessEntity
    - OpenDate
    - InitialContext
    - EndContext
    - Activities : TraceActivity[]

    CaseContext
    - ContactOwnerId
    - QuoteOwnerId
    - Terms

InitialContext is publication context.

EndContext is the endpoint context of the path represented by that Digest. For RestoreSnapshot it is the abandoned path endpoint.

Open/Terminal is derived from the activity chronology; it is not redundantly encoded by separate OpenTrace / TerminalTrace wrappers.

## 10. Flat Trace activity chronology

TraceActivity is an ordered semantic digest.

Important variants include:

- FirstPresentation;
- Presentation;
- RepricingRequested;
- RepricingRequestedOnAway;
- QuoteOwnerChanged;
- PricingDateRolled;
- AssumedTradeDateChanged;
- ValidityExtended;
- HitClose;
- AwayClose;
- UnpresentedClose;
- Cancelled;
- Reopened.

Terminal occurrences are first-class activities. A genuine terminal followed by Reopen therefore remains visibly present in later Digests:

    Presentation
    AwayClose
    Reopened
    Presentation
    HitClose

Reopen does not contain the prior terminal as nested payload. The terminal and Reopen are independent ordered business occurrences.

Restore is intentionally absent from TraceActivity because it is operational path selection rather than genuine business activity.

## 11. First Presentation boundary

FirstPresentation is a distinct Trace activity because it is the Inquiry -> Negotiating boundary.

It carries:

- durable Terms;
- ContactOwnerId;
- Presentation pricing context;
- Quote;
- Presentation.

This records the Terms/context under which customer negotiation first began.

Same-Case Notional / SettlementDateRule changes are prohibited after first Presentation. There is therefore no artificial FixTerms activity.

Later Presentation activities do not repeat Terms/ContactOwner merely because the first one did.

## 12. Compression rules

Trace remains a digest, not a full event log.

Before FirstPresentation:

- ordinary pricing/context changes are compressed;
- this remains true even after a pre-Presentation Cancel/Reopen cycle;
- FirstPresentation records the relevant boundary context.

Lifecycle milestones are never compressed merely because no Presentation has yet occurred:

- UnpresentedClose;
- Cancelled;
- Reopened.

After FirstPresentation, the selected pricing/customer semantic changes retained by the canonical model are kept in ordered TraceActivity form.

Terminal activities do not duplicate their historical CaseContext. The milestone TraceRecord created at that time remains durable and holds that endpoint context.

## 13. History resolution and retention

TraceRecord construction resolves Case-local IDs to durable business values while RfqCaseHistory is available.

Reopen state construction itself uses the current TerminalPricingContext.

Reopened TraceRecord construction is different: it is independently materialized from retained operational History. It is not copied from the prior terminal TraceRecord.

This preserves audit value: the terminal-time representation and reopen-time representation are independent durable observations of the resolved chronology.

There is no fallback from missing History to copying an older TraceRecord.

Retention rules therefore preserve operational History while any supported history-dependent operation still needs it:

- Open Cases retain enough History for the next terminal/Restore TraceRecord;
- terminal Cases retain enough History for the supported Reopen/Restore window;
- after Reopen, pre-Reopen History remains retained while the Case is Open so a later terminal/Restore digest can still be constructed from History alone.

The Reopen window is operational/Persistence policy, not a Domain chronology invariant.

## 14. Revision + Trace API

Open-only operations return the next revision.

Terminal, Reopen, and Restore operations return:

    RevisionTraceResult
    - Revision
    - Trace : TraceRecord

with operation-specific historical context inputs:

- TerminalTraceContext;
- ReopenTraceContext;
- RestoreTraceContext.

Do not collapse those contexts into one large generic context merely because the result shape is shared.

Revision + TraceRecord produced by one Domain operation should be persisted atomically.

## 15. Bridge to historical correction

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

## 16. Canonical incorporation

The decisions in this note are incorporated into `../../../domain.md`.

Design Sessions 05 and 06 remain historical rationale for the operational-history and first-class-operation foundations. Session 07 records the Reopen and CaseActivityDigest refinement.

Historical correction replay source/path selection remains the active next design problem.
