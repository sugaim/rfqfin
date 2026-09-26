# Case History, Restore, Trace, and Replayable Operations

## Status

Active retained rationale for the canonical RfqCaseRevision / Operational Restore / TraceData foundation.

The canonical authority is `../../../domain.md`. This note explains why the current model has its shape and records the bridge into historical correction.

The earlier abstract correction-history model is not the starting point for current work.

## 1. Layered model

The current model separates four concepts:

    RfqCase
      = one complete valid business state

    RfqCaseRevision
      = one immutable operational occurrence of that state

    RfqCaseHistory
      = retained chronology of revisions for one CaseId

    TraceData
      = durable self-contained business representation
        materialized from operational history

RfqCase does not itself mean "the latest operational record."

RfqCaseHistory is a Domain chronology concept, not a requirement to eagerly load one in-memory collection.

TraceData may outlive RfqCaseHistory, so it cannot depend on Case-local child IDs or version chronology for interpretation.

## 2. First-class ordinary Case operations

The positive-flow model now treats ordinary business operations as first-class Domain Objects.

Conceptually:

    Apply :
        RfqCase x CaseOperation
        -> RfqCase

This is a partial function: every operation has documented valid source-state shapes and invariants.

The operation definition is canonical. The state graph is retained as a state-centric view of the same semantics because it is useful for understanding lifecycle reachability.

Current hierarchy:

    CaseOperation
    = Open(OpenOperation)
    | Terminal(TerminalOperation)

The same fully resolved CaseOperation value is used for live positive flow and retained/replayed operational history.

Application request models are separate. Application resolves:

- current authenticated actor into the business ActorId where relevant;
- business/action Timepoint and BusinessEntityLocalDate values;
- generated Case-local IDs;
- external calendar/calculation or other context;

then constructs the fully resolved Domain Operation.

A CaseOperation contains every exogenous argument needed to deterministically apply it to a valid source RfqCase. It does not duplicate values that are deterministically derivable from the source RfqCase merely for replay.

This removes the earlier need for a separate replay-only CaseCommand representation.

## 3. Revision transition provenance

Canonical transition provenance is:

    RfqCaseTransition
    = Published(DraftId)
    | Applied(CaseOperation)
    | RestoredFrom(TargetVersion)

Published creates the initial revision.

For every accepted ordinary Domain Operation after publication:

    next.Version == current.Version.Next()
    next.CaseId == current.CaseId
    next.Transition == Applied(operation)

Rejected operations and pure Application/UI work create no revision.

One accepted Domain Operation creates exactly one next revision. An Application composite may therefore create several revisions even when persistence commits them atomically.

Applied(CaseOperation) stores the same fully resolved Domain Operation used by live processing, including generated identities and supplied actor/time/date values required for deterministic replay.

## 4. Operational Restore

Operational Restore means abandoning the currently effective operational path and returning to a previously valid Open Case state so ordinary positive flow can continue.

Given:

    v10 = earlier Open revision
    ...
    v20 = current revision

Restore(v10) creates:

    v21
    - Version = v20.Version.Next()
    - Case = v10.Case
    - Transition = RestoredFrom(v10.Version)

The target:

- belongs to the same Case chronology;
- is earlier than current;
- is Open;
- may itself belong to a path previously superseded.

The current revision may be Open or Terminal.

Terminal revisions are not Restore targets.

Case-local child identities from the selected target revision are reused exactly.

Restore is not an inverse CaseOperation and does not remove the mistaken path from operational chronology.

A genuinely correct Away/Close followed later by renewed customer interest is not automatically Restore. Restore represents correction of a mistaken operational path; renewed business after a correct terminal outcome has different meaning.

## 5. Operation API boundary and terminal Trace

The pure underlying business semantics can be reasoned about as:

    RfqCase x OpenOperation
        -> Open RfqCase

    RfqCase x TerminalOperation
        -> Terminal RfqCase

but this does not imply that every internal factorization should become public API.

Ordinary Open processing is conceptually:

    ApplyOpen(
        currentRevision,
        openOperation
    ) -> nextRevision

Terminal processing is:

    ApplyTerminal(
        currentRevision,
        terminalOperation,
        terminalTraceContext
    ) -> TerminalResult

    TerminalResult
    - Revision
    - Trace

terminalTraceContext supplies the retained/resolved historical facts and Trace-recording context needed for materialization.

The exact loaded representation is implementation-specific.

The important API invariant is that callers should not casually be able to:

- create a terminal revision while omitting its Trace;
- materialize TraceData from arbitrary snapshots disconnected from the Domain operation that makes the representation effective.

Revision and Trace produced by one terminal operation should be persisted atomically.

Restore similarly returns Revision + one superseding Trace.

## 6. Current terminal-operation model

Terminal operations are:

    TerminalOperation
    = CloseCase
    | Cancel

CloseCase contains:

    CaseClose
    - CloseDate
    - ClosedBy
    - ClosedAt

    CloseOutcome
    = Hit(HitDate, HitAt)
    | Away(AwayClosure)
    | Unpresented(Feedback?)

    AwayClosure
    = AlreadyRecorded
    | RecordNow(AwayDate, Feedback?)

Hit and Away are Presentation outcomes.

Hit is terminal in normal positive flow.

Away is not necessarily terminal. It may already have been recorded by RequestRepricingOnAway and followed by another pricing round.

For CloseCase(Away):

- AlreadyRecorded reuses the latest Presentation's existing immutable Away outcome;
- RecordNow establishes the Away outcome as part of closure.

These are not two business kinds of Away. They only describe whether the Away fact already exists in the source state.

## 7. Away recording semantics

PresentationAwayOutcome now carries:

    PresentationAwayOutcome
    - PresentationId
    - AwayDate
    - RecordedBy
    - RecordedAt
    - Feedback?

AwayDate is the business-effective local date attributed to the Away outcome.

RecordedBy / RecordedAt describe the internal actor and absolute time at which that Away fact was established in the Domain. They do not claim that this actor caused the client to go Away or that RecordedAt is the exact external customer-event time.

For RequestRepricingOnAway:

    RecordedBy = RequestedBy
    RecordedAt = RequestedAt

For CloseCase(Away.RecordNow):

    RecordedBy = ClosedBy
    RecordedAt = ClosedAt

No separate AwayAt is currently modeled.

## 8. Quote invalidation and validity extension

ExpireQuote is no longer a separate operation.

    QuoteInvalidationReason
    = Expired
    | Withdrawn

    InvalidateQuote
    - InvalidatedBy
    - InvalidatedAt
    - Reason

Expired requires:

    InvalidatedAt >= current ValidUntil

Withdrawn has no expiry-time precondition.

Clock passage alone never mutates RfqCase.

ExtendValidity is:

    ExtendValidity
    - NewValidUntil
    - ExtendedBy
    - ExtendedAt

with:

    NewValidUntil > CurrentValidUntil

Extension may occur after the old ValidUntil has already elapsed.

No current invariant relates ExtendedAt to NewValidUntil.

The broader operation design intentionally keeps operation-specific actor/time names rather than introducing a generic Stamp type, because these Timepoint/ActorId values are logical Domain facts rather than technical audit metadata.

## 9. PricingEpisode provenance

The operation that records an Away and begins the next pricing round is:

    RequestRepricingOnAway

The new PricingEpisode uses:

    PricingEpisodeOrigin.Away(
        PreviousPricingEpisodeId,
        PresentationAwayOutcome
    )

This distinction is intentional:

- operation names describe business actions;
- PricingEpisodeOrigin names describe the business fact from which an Episode arose.

Ordinary RequestRepricing remains distinct and applies to an unpresented current FirmQuote.

## 10. Durable TraceData

TraceData is a compact durable business-activity representation, not a copy of the operational log.

Top-level metadata:

    TraceData
    - RecordedBy
    - RecordedAt
    - RecordedBusinessDate
    - Origin
    - Representation

These Recorded* fields describe production of this TraceData record and are distinct from business-event/action timestamps inside the represented activity.

Current state structure:

    TraceState
    = Effective(TerminalTrace)
    | Superseded(
          Snapshot : TraceSnapshot,
          Reason : SupersessionReason
      )

    TraceSnapshot
    = Open(OpenTrace)
    | Terminal(TerminalTrace)

    TerminalTrace
    = PresentedClosed(PresentedClosedTrace)
    | UnpresentedClosed(UnpresentedClosedTrace)
    | Cancelled(CancelledTrace)

Effective is necessarily terminal.

Superseded may contain the Open or Terminal representation that ceased to be effective.

A superseding Trace repeats the business representation rather than linking to an older TraceData by TraceId.

## 11. Trace activity retention

Trace detailed customer/pricing activity starts at the first Presentation.

Before first Presentation, pricing/workflow changes are intentionally compressed away.

After first Presentation, current ActivityChange variants are:

    ActivityChange
    = RepricingRequested(...)
    | RepricingRequestedOnAway(...)
    | QuoteOwnerChanged(...)
    | PricingDateRolled(...)
    | AssumedTradeDateChanged(...)
    | ValidityExtended(...)

ActivityChange uses past-tense business-fact names. It is not a CaseOperation copy.

For activities retained in TraceData, actor/time is retained with the activity.

Current important payload choices:

    RepricingRequested(
        RejectedQuoteValue,
        Feedback?,
        RequestedBy,
        RequestedAt
    )

    RepricingRequestedOnAway(
        PreviousQuoteValue,
        AwayDate,
        Feedback?,
        RequestedBy,
        RequestedAt
    )

    QuoteOwnerChanged(
        PreviousQuoteOwnerId,
        ChangedBy,
        ChangedAt
    )

    PricingDateRolled(
        PreviousPricingDate,
        RolledBy,
        RolledAt
    )

    AssumedTradeDateChanged(
        PreviousAssumedTradeDate,
        ChangedBy,
        ChangedAt
    )

    ValidityExtended(
        PreviousValidUntil,
        NewValidUntil,
        ExtendedBy,
        ExtendedAt
    )

TraceQuote still materializes EffectiveValidUntil.

ValidityExtended is retained only after the first Presentation; pre-Presentation extension history remains compressed.

ReplaceFirmQuote and InvalidateQuote are currently not retained as separate ActivityChange values.

These are deliberate compression choices, not claims of semantic equivalence.

## 12. Identity resolution and retention

TraceData removes Case-local:

- RfqTermsId;
- PricingEpisodeId;
- QuoteId;
- PresentationId.

Construction resolves those identities to durable business values while operational history is available.

Repeated use of one immutable Case-local ID across revisions must resolve to the same value.

Some Trace facts, such as EffectiveValidUntil and ordered ActivityChange, are chronological rather than simple ID lookups.

Operational history may have finite retention. It must remain available while a still-supported history-dependent operation requires it to construct TraceData.

## 13. Bridge to historical correction

The ordinary replay language is now concrete:

    initial Published revision
      + Applied(CaseOperation)
      + RestoredFrom provenance

Historical correction may reuse ordinary CaseOperation semantics while the corrected business history remains expressible by RfqCase.

Replay is a construction/validation mechanism; it is not automatically a claim that a corrected operation sequence literally happened.

The remaining historical-correction design problems are:

- selecting the correct replay source/path when Restore/superseded paths exist;
- defining allowed edits to the retained CaseOperation program;
- determining the boundary where ordinary RfqCase replay is no longer expressive;
- defining the minimal Trace-side correction state;
- defining any EffectiveCommand language;
- materializing Superseded + corrected Effective Trace records;
- correction-of-correction;
- deciding whether any explicit adequacy relation is needed.

Do not restart from the old Supports(H_rep, H_true) abstraction.

## 14. Domain/Application/Persistence boundary

Domain owns:

- CaseOperation business validity and deterministic meaning;
- resulting RfqCase;
- Version.Next() and RfqCaseTransition provenance;
- Restore business semantics;
- Trace business representation for terminal/Restore operations.

Application owns:

- authorization;
- CurrentUser resolution;
- external context/calendar/calculation;
- ID allocation before constructing fully resolved Domain Operations;
- orchestration/composites.

Persistence owns:

- stale-write protection;
- physical storage;
- atomic append of Domain-produced Revision + Trace facts.

External customer communications, notifications, booking, and other irreversible side effects are not reversed automatically by Restore or historical correction.

## 15. Canonical incorporation

The decisions in this note are incorporated into `../../../domain.md`.

The completed operational-history foundation from Design Session 05 remains useful historical rationale. The first-class CaseOperation refinement and related positive-flow/Trace changes are recorded separately in Design Session 06.

Historical correction itself remains the active next design unit.
