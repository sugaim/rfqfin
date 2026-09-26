# Design Session 07 — Reopen and Activity-Digest Trace

## Goal

Resolve the positive-flow Reopen question before historical-correction replay design, then reconcile durable Trace semantics with genuine terminal -> reopen -> later terminal lifecycles.

The design questions were:

- when a genuinely correct terminal Case may later reopen;
- how Reopen differs from Operational Restore;
- what pricing context survives terminal state;
- how Reopen creates a new PricingEpisode;
- how cancellation semantics affect Reopen eligibility;
- how durable Trace should represent intermediate genuine terminal occurrences and Reopen without hiding or nesting them;
- how Trace compression and operational-history retention should work after Reopen.

## Reopen is genuine positive flow

Reopen is now a first-class ordinary CaseOperation.

    CaseOperation
    = Open(OpenOperation)
    | Terminal(TerminalOperation)
    | Reopen(ReopenOperation)

with:

    OpenOperation     : Open -> Open
    TerminalOperation : Open -> Terminal
    ReopenOperation   : Terminal -> Open

Reopen uses RfqCaseTransition.Applied(Reopen(...)).

Operational Restore remains separate and uses RestoredFrom(TargetVersion).

The semantic distinction is:

- Reopen: the terminal occurrence was genuine and remains part of business history; the same negotiation context later resumes.
- Restore: the prior operational path was mistaken; an earlier valid Open revision is selected instead.

The same CaseId continues on Reopen. Whether later activity belongs to the same negotiation context is an explicit business assertion rather than an inference from dates or matching fields.

## Reopen applicability

Reopen was subsequently simplified to one flat ordinary business operation:

    ReopenOperation
    - NewPricingEpisodeId
    - ReopenDate : BusinessEntityLocalDate
    - NewAssumedTradeDate : BusinessEntityLocalDate
    - ReopenedBy : ActorId
    - ReopenedAt : Timepoint

The earlier ReopenAway / ReopenUnpresented / ReopenCancellation variants were removed. Their distinction was not caller intent: the source Terminal state already determines the applicable reopen provenance and resulting Open state.

Applicability:

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

## Cancellation reasons

The positive-flow CancellationReason taxonomy was deliberately kept minimal:

    CancellationReason
    = Withdrawn
    | CreatedInError

Withdrawn means a genuine Case was stopped and may later resume.

CreatedInError includes duplicate or mistaken creation cases and is not reopenable through ordinary positive flow.

Future operational/statistical requirements may refine the non-reopenable taxonomy.

## Terminal pricing context

The earlier working name PricingContinuation was rejected as purpose-driven.

Terminal state now retains a logically meaningful:

    TerminalPricingContext
    - PricingEpisode
    - PriorPresentation?

    PriorPresentation
    - Presentation
    - AwayOutcome?

This represents the pricing context from which the Case terminated. Reopen consumes it, but Reopen is not its reason for existing.

All terminal states retain TerminalPricingContext, including Hit.

A FirmQuote is never retained in TerminalPricingContext.

Cancellation preserves prior customer Presentation context when one exists; cancellation does not erase customer-facing business facts.

## Reopen always starts a fresh pricing round

Every Reopen creates a new PricingEpisode.

The operation explicitly supplies:

- NewPricingEpisodeId;
- ReopenDate;
- NewAssumedTradeDate;
- ReopenedBy;
- ReopenedAt.

The new Episode:

- keeps current RfqTerms;
- keeps the previous QuoteOwner;
- sets PricingDate=ReopenDate;
- uses explicit NewAssumedTradeDate;
- carries no prior FirmQuote.

PricingEpisode origins are:

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

Application/UI may default NewAssumedTradeDate based on the prior Episode, but Domain receives the resolved explicit value.

No Domain chronology invariant requires ReopenDate / ReopenedAt to be after the preceding terminal date/time.

## Trace redesign: activities are the primary representation

The initial attempt was to keep the old outer TerminalTrace structure and add a Reopened Trace state.

That became awkward once a Case could have:

    Presentation
    -> genuine terminal
    -> Reopen
    -> Presentation
    -> genuine terminal

The intermediate terminal occurrence is itself important business activity and should not be hidden inside Reopen payload.

The Trace model was therefore simplified around an ordered business activity digest.

The durable outer record is:

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

The digest is:

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

Open / Terminal is no longer duplicated as an outer Trace type. It is derived from the ordered activity chronology.

## Flat TraceActivity chronology

The old ActivityChange nesting and ChangesBeforePresentation / ChangesBeforeTerminal organization were removed.

TraceActivity is an ordered semantic chronology containing:

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

Terminal activities are first-class activity occurrences.

Reopen is also a first-class business activity.

Restore is not a TraceActivity because Restore is operational path selection rather than genuine business activity.

This permits a later digest to represent:

    FirstPresentation
    AwayClose
    Reopened
    Presentation
    HitClose

without recursively embedding old TerminalTrace values.

## First Presentation is the negotiation boundary

FirstPresentation is distinct from later Presentation.

It carries:

- Terms;
- ContactOwnerId;
- PresentationPricingContext;
- Quote;
- Presentation.

This matches the positive-flow invariant that Notional / SettlementDateRule may change only before the first customer Presentation.

First Presentation is therefore the real business boundary at which those Terms become fixed for the same Case.

A separate artificial FixTerms activity was rejected.

AssumedTradeDate remains PricingEpisode context rather than RfqTerms. It may change after Presentation through a fresh PricingEpisode and quote reaffirmation.

## Initial and endpoint context

CaseActivityDigest carries:

    InitialContext
    EndContext

Each contains:

- ContactOwnerId;
- QuoteOwnerId;
- TraceTerms.

InitialContext is publication context.

EndContext is the endpoint of the represented operational path.

For RestoreSnapshot, EndContext is specifically the abandoned path endpoint immediately before Restore, not the restored target state.

Historical terminal activities do not duplicate their full CaseContext inside later Digests. Their own milestone TraceRecords remain durable.

## Trace compression

Trace remains a semantic digest rather than a complete event log.

Before FirstPresentation:

- ordinary pricing/context changes are compressed;
- this remains true even after a pre-Presentation Cancel/Reopen cycle.

However lifecycle milestones are retained even before FirstPresentation:

- UnpresentedClose;
- Cancelled;
- Reopened.

After FirstPresentation, the selected pricing/customer semantic activities retained by the existing Trace policy remain in ordered TraceActivity form.

ReplaceFirmQuote and InvalidateQuote remain compressed under the current policy.

## Restore snapshot semantics

Restore produces:

    TraceRecordKind.RestoreSnapshot

The Digest represents the abandoned effective operational path immediately before Restore.

Restore itself is not inserted into Activities.

Restore does not create a second TraceRecord for the restored Open target.

This keeps three concerns separate:

- RfqCaseHistory / RfqCaseTransition: operational chronology and RestoredFrom provenance;
- TraceRecord: why a durable digest was materialized;
- CaseActivityDigest: genuine business activity represented by that record.

## Reopen Trace materialization and retention

Reopen state construction uses the current TerminalPricingContext.

Reopened TraceRecord construction is independently resolved from retained RfqCaseHistory.

It is not copied from the earlier terminal TraceRecord.

This preserves evidentiary value: the terminal-time and reopen-time durable records are independent materializations of the operational chronology.

There is no fallback from missing History to copying an older TraceRecord.

Retention policy therefore keeps enough chronology while any supported history-dependent operation still needs it:

- Open Case: retain enough for later terminal/Restore Trace;
- terminal Case: retain enough for supported Reopen/Restore window;
- after Reopen: retain pre-Reopen chronology while Open so later terminal/Restore can again be built from History alone.

The Reopen support window is operational/Persistence policy, not a Domain date invariant.

## Revision-level API

Open-only operations remain revision-local.

Terminal, Reopen, and Restore use one common result shape:

    RevisionTraceResult
    - Revision
    - Trace : TraceRecord

A later API refinement made RfqCaseHistory the single revision-level source for current state and retained chronology, and made Trace recording metadata explicit and shared:

    TraceContext
    - RecordedBy : ActorId
    - RecordedAt : Timepoint
    - RecordedBusinessDate : BusinessEntityLocalDate

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

Operational Restore is represented separately from CaseOperation:

    RestoreOperation
    - TargetVersion : CaseVersionNumber

    ApplyRestore(
        history : RfqCaseHistory,
        operation : RestoreOperation,
        traceContext : TraceContext
    ) -> RevisionTraceResult

CaseVersionNumber supports New(), Next(), and Prev(); New() creates the initial Case version and Prev() has no value at the initial version.

Revision + TraceRecord produced by one Domain operation should be persisted atomically.

## Naming refinements

The durable business representation is now called CaseActivityDigest.

TraceData was renamed to TraceRecord to distinguish the durable record from the contained activity digest.

The Case/RfqCase naming rule remains:

- aggregate and top-level operational chronology types use RfqCase where independent identification matters;
- Case-scoped vocabulary may use the shorter Case prefix when unambiguous.

This supports names such as:

- RfqCase;
- RfqCaseRevision;
- RfqCaseHistory;
- CaseOperation;
- CaseActivityDigest.

## Canonical changes

This design unit updated:

- `../../domain.md`;
- `../topics/correction/case-history-and-trace.md`;
- `../topics/correction/correction-cases.md`.

`../handoff.md` was intentionally not updated because the active discussion continues in the same conversation/session context.

## Why this design unit is complete

The positive-flow prerequisite that blocked historical-correction replay design is now resolved:

- genuine Reopen semantics are explicit;
- Reopen/Restore are sharply separated;
- cancellation eligibility is explicit;
- terminal pricing lineage is explicit;
- Reopen PricingEpisode provenance is explicit;
- durable Trace can represent repeated terminal/Reopen cycles without recursive terminal snapshots;
- Trace compression and retention rules remain coherent.

No remaining positive-flow contradiction was found in the canonical rescan.

## Next design unit

Historical correction replay source/path selection.

Start from retained:

    Published
    Applied(CaseOperation)
    RestoredFrom

and determine semantic ancestry/path selection when physical revision chronology contains Restore edges and paths that were once effective but later abandoned.

In particular, determine:

- which replay program represents the current effective path;
- how RestoredFrom participates in semantic ancestry;
- how to reconstruct an abandoned historical path for correction;
- how correction targets a business occurrence on a path that is no longer current.

Do not restart from the earlier abstract Supports(H_rep, H_true) model.
