# Design Session 06 — First-Class RfqCase Operations

## Goal

Turn the previously implicit/replay-deferred ordinary operation model into an explicit canonical Domain model before continuing historical-correction replay design.

The design questions were:

- what an ordinary RfqCase operation is;
- whether live positive flow and retained/replayed history should use the same Domain value;
- what each operation must carry for deterministic application;
- how terminal operations relate to Trace materialization;
- how the state graph should coexist with a function-like operation model;
- which positive-flow names/invariants needed refinement once operations became first-class.

## Main result

Ordinary RfqCase operations are now first-class Domain Objects.

Conceptually:

    Apply :
        RfqCase x CaseOperation
        -> RfqCase

This is a partial function. Each operation has explicit valid source-state shapes and invariants.

The operation definition is canonical.

The state graph remains as a state-centric cognitive/reference view and the applicability table records valid source/target shapes.

## Live and replay use the same Domain Operation

Application request models are not replay records.

Application resolves external context first:

    raw request
      + current actor
      + business date/time
      + generated IDs
      + external context
        -> fully resolved CaseOperation

The same fully resolved CaseOperation is then used by:

- live positive-flow application;
- RfqCaseRevision.Applied provenance;
- future retained-history replay/correction.

No separate replay DTO / CaseCommand language is needed for ordinary RfqCase semantics.

An operation carries every exogenous input required for deterministic application, but does not duplicate values deterministically available from the source RfqCase.

## Operation hierarchy

Current hierarchy:

    CaseOperation
    = Open(OpenOperation)
    | Terminal(TerminalOperation)

PublishDraft is separate because it creates the initial Published revision.

Operational Restore is separate because it selects an earlier Open revision rather than applying an ordinary business operation to the current Case.

## Open operations

Canonical OpenOperation set:

- CommitQuote;
- ReplaceFirmQuote;
- InvalidateQuote;
- RequestRepricing;
- ChangeRfqTerms;
- ChangeQuoteOwner;
- RollPricingDate;
- ChangeAssumedTradeDate;
- PresentQuote;
- ExtendValidity;
- RequestRepricingOnAway;
- ChangeContactOwner.

Generated Case-local IDs are supplied by the operation when a new immutable child Entity is created.

Previous/current values and IDs are omitted when source RfqCase determines them.

## Actor/time values

Accepted operation actor/time values are Domain business facts, not generic persistence audit metadata.

The model deliberately uses operation-specific vocabulary:

- CommittedBy / CommittedAt;
- PresentedBy / PresentedAt;
- InvalidatedBy / InvalidatedAt;
- RequestedBy / RequestedAt;
- ChangedBy / ChangedAt;
- RolledBy / RolledAt;
- ExtendedBy / ExtendedAt;
- ClosedBy / ClosedAt;
- CancelledBy / CancelledAt.

A generic OperationStamp was considered and rejected because it made logical business timestamps look like system/audit timestamps and obscured the meaning of values such as HitAt.

## Quote invalidation

ExpireQuote was removed as a separate operation.

    QuoteInvalidationReason
    = Expired
    | Withdrawn

InvalidateQuote carries InvalidatedBy / InvalidatedAt / Reason.

Expired requires:

    InvalidatedAt >= current ValidUntil

Withdrawn has no expiry-time precondition.

The trigger differs, but the Domain effect is one quote invalidation operation with a typed reason.

## Validity extension

ExtendValidUntil was renamed to ExtendValidity.

    ExtendValidity
    - NewValidUntil
    - ExtendedBy
    - ExtendedAt

Current invariant:

    NewValidUntil > CurrentValidUntil

Extension is allowed after the previous ValidUntil has elapsed.

No ordering relation between ExtendedAt and NewValidUntil is currently imposed.

## Repricing after Away

ContinueAfterAway was replaced by:

    RequestRepricingOnAway

This better describes the business action: the Contact-side workflow requests another pricing round when the current customer Presentation becomes Away.

The operation:

- establishes PresentationAwayOutcome;
- creates a new PricingEpisode;
- uses PricingEpisodeOrigin.Away(...).

The operation and Trace activity use action-oriented / past-tense names respectively:

    Operation:
        RequestRepricingOnAway

    ActivityChange:
        RepricingRequestedOnAway

    PricingEpisodeOrigin:
        Away(...)

## Away recording semantics

PresentationAwayOutcome is now:

    PresentationAwayOutcome
    - PresentationId
    - AwayDate
    - RecordedBy
    - RecordedAt
    - Feedback?

AwayDate is the business-effective date attributed to the Away outcome.

RecordedBy / RecordedAt describe the internal actor/time at which the Away fact was established in the Domain. They do not claim that the actor caused the client to go Away or that RecordedAt is the exact customer-event time.

RequestRepricingOnAway derives:

    RecordedBy = RequestedBy
    RecordedAt = RequestedAt

CloseCase(Away.RecordNow) derives:

    RecordedBy = ClosedBy
    RecordedAt = ClosedAt

No separate AwayAt is modeled.

## Terminal operations and Close model

Hit and CloseAway were reorganized under one normal close operation.

    TerminalOperation
    = CloseCase
    | Cancel

    CloseCase
    - Close : CaseClose
    - Outcome : CloseOutcome

    CloseOutcome
    = Hit(HitDate, HitAt)
    | Away(AwayClosure)
    | Unpresented(Feedback?)

    AwayClosure
    = AlreadyRecorded
    | RecordNow(AwayDate, Feedback?)

AlreadyRecorded versus RecordNow is not a distinction between business kinds of Away. It only describes whether the source Case already contains the relevant Away fact or whether the CloseCase operation establishes it.

Hit remains special in business semantics because Hit is terminal in normal flow, whereas Away may be recorded earlier and followed by another pricing round.

A genuinely correct terminal Away/Close followed later by renewed customer interest is not automatically Operational Restore.

## Terminal API boundary

The underlying state transition can be reasoned about as:

    RfqCase x TerminalOperation
        -> Terminal RfqCase

but this decomposition need not be exposed as public API.

The revision-level conceptual API is:

    ApplyTerminal(
        currentRevision,
        terminalOperation,
        terminalTraceContext
    ) -> TerminalResult {
         Revision,
         Trace
       }

This preserves the invariant that terminal accepted processing produces its Revision and durable Trace together.

The design intentionally avoids committing to a public API that can:

- create terminal state without Trace;
- materialize Trace from arbitrary snapshots.

Internal transition kernels and Trace materializers may still be factored separately.

## Trace refinement

Trace state nesting was simplified to:

    TraceState
    = Effective(TerminalTrace)
    | Superseded(TraceSnapshot, Reason)

    TraceSnapshot
    = Open(OpenTrace)
    | Terminal(TerminalTrace)

This removes the earlier Superseded(Effective(...)) double-effective vocabulary.

Trace ActivityChange uses past-tense business facts.

Current retained post-Presentation activity:

- RepricingRequested;
- RepricingRequestedOnAway;
- QuoteOwnerChanged;
- PricingDateRolled;
- AssumedTradeDateChanged;
- ValidityExtended.

For activity facts retained in TraceData, actor/time is retained with the activity.

ValidityExtended is now retained after first Presentation. Pre-Presentation extension history remains compressed.

Trace remains a business-semantic digest rather than a complete operation log.

## Revision provenance

RfqCaseTransition is now:

    RfqCaseTransition
    = Published(DraftId)
    | Applied(CaseOperation)
    | RestoredFrom(TargetVersion)

CaseCommand is no longer part of the canonical model.

Applied stores the same fully resolved Domain Operation used by live processing.

## Documentation structure

The canonical operation definition is operation-first.

The applicability table gives each operation's valid source/result state shapes.

The state graph remains as a derived/state-centric view because it is useful for human reasoning.

Repeated graph edges with the same operation name are not distinct Domain operation types.

## Canonical changes

This design unit updated:

- `../../domain.md`;
- `../topics/correction/case-history-and-trace.md`;
- `../topics/correction/correction-cases.md`.

The previous Design Session 05 record remains unchanged as historical rationale for the state of the model at that checkpoint.

## Why this design unit is complete

The questions that previously blocked historical replay design are now answered:

- ordinary operation meaning is explicit;
- live/replay operation representation is unified;
- operation payload principles are explicit;
- operation actor/time semantics are explicit;
- Open and Terminal operation sets are concrete;
- terminal Trace API boundary is defined conceptually;
- positive-flow graph and operation-first semantics have a clear authority relationship;
- stale operation names/invariants have been reconciled.

## Next design unit

Historical correction replay source/path selection.

Start from retained:

    Published
    Applied(CaseOperation)
    RestoredFrom

chronology and determine which effective/historical path is the source program for a correction when Restore and superseded paths are present.

After that, determine allowed edits to the ordinary CaseOperation program and the boundary to Trace-native correction semantics.
