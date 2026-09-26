# Design Session 05 — Operational History, Restore, and Trace

## Goal

Complete the operational-correction design unit far enough to provide a stable Domain foundation for the later historical-correction discussion.

The starting question was narrow:

> when an active/same-day Case follows a mistaken operational path, how can it return to a prior valid state and continue ordinary positive flow?

The discussion expanded only where that requirement exposed necessary Domain concepts:

- immutable operational revisions;
- restore provenance;
- durable business-activity TraceData;
- actor/timestamp facts needed by Trace;
- the lifetime separation between operational history and durable Trace;
- replayable accepted commands as a bridge to historical correction.

## Main result

The model now separates four concepts.

    RfqCase
      = one complete valid business state

    RfqCaseRevision
      = one immutable operational occurrence of that state

    RfqCaseHistory
      = retained chronology of revisions for one CaseId

    TraceData
      = durable self-contained business representation
        materialized from history and allowed to outlive it

This removes the earlier ambiguity where RfqCase itself implicitly meant both the business state and the current operational record.

## Revision chronology

The canonical working shape is:

    RfqCaseRevision
    - CaseId
    - Version : CaseVersionNumber
    - Case : RfqCase
    - Transition : RfqCaseTransition

with:

    RfqCaseRevision.CaseId == RfqCaseRevision.Case.CaseId

and:

    RfqCaseTransition
    = Published(DraftId)
    | Applied(CaseCommand)
    | RestoredFrom(TargetVersion)

PublishDraft creates the initial Published(DraftId) revision.

Every accepted ordinary Domain operation after publication creates exactly one revision at:

    current.Version.Next()

Rejected operations and pure UI/Application work do not create revisions.

An Application composite that executes several accepted Domain operations therefore produces several revisions even if persistence commits them atomically.

## Operational Restore

Operational Restore is Domain behavior, not an Application reconstruction trick.

Its business meaning is:

> return to a previously valid Open Case state, then continue ordinary positive-flow processing from that state as a new chronological revision.

Restore may be invoked while current is Open or Terminal.

The target:

- must belong to the same Case chronology;
- must be earlier than current;
- must be Open;
- may itself be on a path that was previously superseded.

Terminal revisions are deliberately not Restore targets. Reinstating an old terminal outcome would have different business meaning and is not currently modeled as operational Restore.

Example:

    v10 = earlier Open
    ...
    v20 = mistaken current path

    Restore(v10)

    v21
    - Version = v20.Version.Next()
    - Case = v10.Case
    - Transition = RestoredFrom(v10.Version)

The exact Case-local identities present in v10 are reused. The mistaken path remains immutable chronology.

No explicit Branch/Worldline Domain object is required.

## Concurrency and external effects

Stale/concurrent correction requests are Application/Persistence concerns.

A restore observed against v20 should commit only if v20 is still current.

Restore does not unsend customer communication or reverse downstream integration/booking side effects. Those require explicit reconciliation outside the state-restore semantics.

## TraceData

Restore exposed a second requirement: operational history is not the durable business record.

RfqCaseHistory may eventually be deleted. TraceData is expected to remain.

Therefore TraceData:

- is self-contained;
- removes Case-local RfqTermsId / PricingEpisodeId / QuoteId / PresentationId references;
- materializes the business values needed for interpretation;
- may keep durable/master identities such as CaseId, ActorId, ClientId, SecurityId, QuoteOwnerId, and ContactOwnerId;
- is intentionally smaller than the full operational log.

Top-level shape:

    TraceData
    - RecordedBy
    - RecordedAt
    - RecordedBusinessDate
    - Origin
    - Representation

    TraceRepresentation
    - CaseId
    - BusinessEntity
    - CaseOpenDate
    - InitialContactOwnerId
    - FirstPresentedContactOwnerId?
    - State

    TraceState
    = Effective(EffectiveTrace)
    | Superseded(SupersededTrace)

Trace records may be appended multiple times for one Case.

A superseding Trace repeats the old business representation as Superseded rather than referencing an older Trace record by TraceId. This is deliberate self-containment.

## Trace generation

Terminal operations produce:

    TerminalResult
    - Revision
    - Trace

Operational Restore likewise produces:

    RestoreResult
    - Revision
    - Trace

Because Restore targets are always Open, it produces exactly one superseding Trace:

- Superseded(Open(...)) when current was Open;
- Superseded(Effective(...)) when current was Terminal.

The later terminal operation after restored processing produces the next Effective Trace.

Revision and Trace results from one Domain operation should be persisted atomically.

## Trace activity compression

Trace begins its detailed pricing/customer-activity history at the first Presentation.

Pre-Presentation pricing/workflow changes are deliberately compressed away.

Special root-level responsibility facts are retained as:

- InitialContactOwnerId;
- FirstPresentedContactOwnerId?.

After the first Presentation, ActivityChange currently preserves:

- ContinuedAfterAway;
- RepricingRequested;
- QuoteOwnerChanged;
- PricingDateRolled;
- AssumedTradeDateChanged.

ReplaceFirmQuote, InvalidateQuote, ExpireQuote, and the sequence of ExtendValidUntil operations are not separately retained in the durable digest.

EffectiveValidUntil is materialized from the operational chronology.

These are information-compression choices, not claims that the omitted operations are semantically identical in positive flow.

## Actor/time refinement

Trace requirements established that some actor/time values are durable business facts rather than generic audit metadata.

Canonical Domain now includes:

- Quote.CommittedBy / CommittedAt;
- QuotePresentation.PresentedBy / PresentedAt;
- ClosedBy / ClosedAt;
- CancelledBy / CancelledAt;
- ActorId as the actual human/service performer.

Responsibility identities remain separate:

- QuoteOwnerId = pricing responsibility;
- ContactOwnerId = customer-contact responsibility.

Actor/time for ExtendValidUntil remains intentionally unmodeled because Trace currently retains only the effective ValidUntil.

## Loading and Aggregate terminology

RfqCaseHistory is a Domain chronology concept, not a requirement to hold or rewrite the full history on every operation.

Typical implementation loading may be:

    ordinary op
      -> current revision only

    restore
      -> current + target + history needed for Trace

    terminal op
      -> current + history needed for Trace

The canonical document no longer relies on the earlier statement that RfqCase itself is the Aggregate Root for all these concerns.

No replacement Aggregate Root label was forced. Repository/loading consistency terminology can be settled with implementation design without changing the Domain roles above.

## Bridge to historical correction

The discussion reached a concrete bridge to the next unit.

While operational history is retained, historical correction may start from:

    initial Published revision's RfqCase
      + recorded Applied(CaseCommand) transitions

and correct/replay the command sequence.

If the business truth cannot be represented by the current RfqCase command language, the working direction is a one-way switch into a Trace-native EffectiveCommand language.

Once that switch occurs, processing should not return to RfqCase commands because the effective-history state may contain facts that no RfqCase state can represent.

Command-based correction is intentionally unavailable once the retained operational history has expired; sufficiently old correction is currently allowed to be unsupported.

The exact CaseCommand payloads and EffectiveCommand model are not part of this completed unit.

## Refinement of earlier correction exploration

The earlier correction foundation explored:

    Supports(H_rep, H_true)

and whole-History replacement as abstract semantics.

That exploration remains useful background, but it is no longer the starting point.

The new concrete state/revision/Trace foundation resolves several assumptions that were previously open:

- revisions are now Domain-significant operational chronology;
- accepted commands are retained as transition provenance;
- TraceData is the durable corrected-business representation;
- operational history and durable business history have different lifetimes.

The old concrete correction-case catalog remains valuable regression material.

Do not restart the next session by trying to formalize Supports. Use concrete correction cases against the new model and introduce an adequacy relation only if a real requirement needs one.

## Canonical changes

This unit was incorporated into `../../domain.md`.

Notable canonical changes:

- RfqCase state versus RfqCaseRevision chronology;
- Published / Applied / RestoredFrom transitions;
- monotone Domain CaseVersionNumber;
- operational Restore;
- TraceData;
- ActorId and durable action timestamps;
- history-dependent terminal/restore outputs;
- finite operational-history retention versus durable Trace lifetime;
- PublishDraft as the sole current Case-creation path.

Working rationale remains in:

- `../topics/correction/case-history-and-trace.md`;
- `../topics/correction/operational-correction.md` (completed/superseded starting note).

## Why this design unit is complete

The original operational-correction questions are answered:

- Restore is Domain behavior;
- target is any earlier same-Case Open revision, not merely the immediately previous revision;
- current Terminal may restore to earlier Open;
- target Case-local identities are reused;
- every accepted ordinary operation creates the next revision;
- restore creates one next revision;
- restore provenance is Domain-significant;
- explicit branch objects are unnecessary;
- stale writes and external side effects remain Application/Persistence/integration concerns.

Trace/Restore may be enhanced later when historical correction exposes a concrete new requirement, but they are sufficiently self-contained to serve as the next unit's foundation.

## Next design unit

Historical correction.

Start from the canonical revision/Trace model and the existing concrete correction case catalog.

The next unit should determine:

- the replayable CaseCommand language and payload requirements;
- how a corrected command sequence is formed and replayed;
- when ordinary replay can no longer represent the intended truth;
- the Trace-side intermediate state;
- EffectiveCommand semantics;
- how a correction produces Superseded and new Effective TraceData;
- correction-of-correction;
- which older abstract preservation/adequacy ideas remain necessary after the concrete model is applied.
