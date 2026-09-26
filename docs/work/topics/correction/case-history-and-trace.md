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

Restore produces one:

    TraceRecord
    Kind = RestoreSnapshot

whose Digest represents the abandoned path immediately before Restore. Digest.EndContext is the abandoned path endpoint, not the restored target.

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

Open-only operations remain revision-local.

Terminal, Reopen, and Restore operations return:

    RevisionTraceResult
    - Revision
    - Trace : TraceRecord

Trace-producing revision-level APIs take RfqCaseHistory as the source of the current revision and retained chronology:

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

The shared Trace recording context is:

    TraceContext
    - RecordedBy : ActorId
    - RecordedAt : Timepoint
    - RecordedBusinessDate : BusinessEntityLocalDate

History is not duplicated inside TraceContext. Supplying History as the revision-level source avoids separately supplied current Case/revision values that could disagree with that chronology.

Revision + TraceRecord produced by one Domain operation should be persisted atomically.

## 15. Working Trace simplification / TraceRevision conclusions awaiting reconfirmation

The discussion after the revision-level API refinement explored a substantially simpler public/business Trace model and a versioned Trace concept.

These conclusions are **working decisions, not yet canonical**. The conversation context became unreliable late in the session, so the next session must re-read canonical material, re-check these decisions explicitly, and only then update `domain.md`.

Current working conclusions to re-check:

- Trace should be the public/analysis-oriented business milestone representation, not a full copy of RfqCaseHistory.
- Candidate simplified TraceActivity set:

      TraceActivity
      = TermsAmended
      | Presented
      | Away
      | InternalRepricingRequested
      | Hit
      | Closed
      | Cancelled
      | Reopened

- Restore remains absent from TraceActivity.
- RequestRepricingOnAway should contribute Away, not InternalRepricingRequested.
- InternalRepricingRequested is for ordinary RequestRepricing and should carry PreviousQuoteValue, optional Feedback, RequestedBy, and RequestedAt.
- Presented should become a self-contained snapshot carrying Terms, ContactOwnerId, QuoteOwnerId, PricingDate, AssumedTradeDate, Quote, PresentationDate, PresentedBy, and PresentedAt.
- The public Trace should not expose PresentationId; Hit/Away association should be resolved from ordered business chronology.
- Hit/Away and Case closure should remain distinct public facts so Away-followed-by-continued-pricing is distinguishable from Away-close.
- InitialContext and EndContext should remain, with CaseContext expanded to Terms, ContactOwnerId, QuoteOwnerId, PricingDate, and AssumedTradeDate.
- TraceRecord should become a versioned Domain concept, tentatively TraceRevision, with an independent chronology from RfqCaseRevision.
- The outer classification should be called Trigger rather than Kind.
- Working trigger shape:

      TraceRevisionTrigger
      = HitClose
      | AwayClose
      | UnpresentedClose
      | Cancelled
      | Reopened
      | Restore(...)
      | HistoricalCorrection(...)

- Restore and HistoricalCorrection triggers should carry actor/time plus a typed CorrectionReason.
- Working CorrectionReason shape:

      CorrectionReason
      - Code : DataError | OperationalError | Other
      - Note : string

The above should not be copied mechanically into canonical documentation. Reconfirm semantics first.

## 16. Next unresolved design questions

After reconfirming the working conclusions above, continue with:

1. TraceRevision version semantics:
   - TraceVersionNumber shape;
   - initial/new version construction;
   - successor/predecessor semantics;
   - how the latest Trace revision is supplied to a Trace-producing Domain operation.

2. Revision-level API after TraceRecord -> TraceRevision:
   - final RevisionTraceResult shape;
   - how ApplyTerminal / ApplyReopen / ApplyRestore obtain the prior Trace revision/version without introducing inconsistent duplicated inputs.

3. HistoricalCorrection API:
   - operational RfqCaseHistory remains immutable;
   - correction appends a new corrected Trace revision;
   - exact source inputs and correction provenance are not yet designed.

4. Representative correction scenarios:
   - wrong value;
   - extra activity;
   - missing activity;
   - wrong activity kind;
   - correction that changes later business history;
   - business truth not expressible by the current RfqCase / CaseOperation model.

5. Construction strategy:
   - ordinary CaseOperation replay where expressive;
   - hybrid replay + correction-native construction;
   - Trace-native construction;
   - handling currently unmodeled business flows.

6. Introduce an ephemeral TraceProjectionState only if the concrete scenarios require it.

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
