# Design Session 08 — Trace API and Revision Checkpoint

## Goal

Refine the revision-level APIs that produce durable Trace, remove unnecessary Reopen operation variants, and establish a clean checkpoint before continuing the Trace simplification / TraceRevision design.

This session also explored a simpler public Trace model and a versioned Trace concept, but those later conclusions are intentionally left non-canonical pending reconfirmation in a fresh session.

## Canonical refinements completed

### ReopenOperation flattened

The previous variants:

    ReopenAway
    ReopenUnpresented
    ReopenCancellation

were removed.

The canonical operation is now one ordinary business operation:

    ReopenOperation
    - NewPricingEpisodeId
    - ReopenDate : BusinessEntityLocalDate
    - NewAssumedTradeDate : BusinessEntityLocalDate
    - ReopenedBy : ActorId
    - ReopenedAt : Timepoint

This is not merely a field-shape deduplication.

The source Terminal state already determines:

- whether the reopen is valid;
- which PricingEpisode provenance is created;
- which Open state results.

The caller therefore does not select a reopen variant.

## Trace-producing revision APIs now use History as source of truth

The earlier APIs accepted a current revision separately from historical Trace context.

That permits redundant inputs whose contents could disagree.

The canonical revision-level Trace-producing APIs now use RfqCaseHistory as the single source of:

- current revision/current RfqCase;
- retained operational chronology required for Trace materialization.

The shared Trace recording context is:

    TraceContext
    - RecordedBy : ActorId
    - RecordedAt : Timepoint
    - RecordedBusinessDate : BusinessEntityLocalDate

The APIs are:

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

Restore remains semantically different from an ordinary CaseOperation.

It now has an explicit revision-level Domain Object:

    RestoreOperation
    - TargetVersion : CaseVersionNumber

and:

    ApplyRestore(
        history : RfqCaseHistory,
        operation : RestoreOperation,
        traceContext : TraceContext
    ) -> RevisionTraceResult

RestoreOperation is not added to CaseOperation.

## CaseVersionNumber refinement

CaseVersionNumber now explicitly supports:

    New()
    Next()
    Prev()

New() creates the initial Case version.

Prev() has no value for that initial version.

This makes adjacent revision navigation explicit without storing a redundant previous-revision field on RfqCaseRevision.

## Canonical documents updated

This completed refinement was incorporated into:

- `../../domain.md`;
- `../topics/correction/case-history-and-trace.md`.

Session 07 was also updated to record the later refinement to its Reopen/API conclusions.

## Working conclusions not yet canonical

The session then explored a larger redesign:

- simplify Trace to a small public/analysis-oriented set of business milestones;
- make Presented self-contained;
- separate Hit/Away from Case closure;
- remove internal projection-only changes from public Trace activities;
- replace TraceRecord with a versioned TraceRevision Domain concept;
- use TraceRevisionTrigger rather than TraceRecordKind;
- represent Restore / HistoricalCorrection as revision triggers with correction provenance.

These working conclusions are summarized in `../topics/correction/case-history-and-trace.md`.

They were **not** incorporated into canonical `domain.md`.

## Why the remaining work is handed off

Late in the discussion, conversational context became mixed: canonical facts, already-decided changes, and new proposals were repeatedly conflated.

To avoid encoding an accidental reinterpretation, the remaining Trace simplification / TraceRevision conclusions must be treated as a working checkpoint rather than blindly applied.

The next session should:

1. read canonical `domain.md` first;
2. read the active topic note;
3. explicitly re-check the pending working conclusions;
4. only after that confirmation update canonical documentation.

## Next design unit

Start with TraceRevision version semantics and the resulting revision-level API.

In particular:

- how TraceRevision is numbered/versioned;
- how a Trace-producing operation obtains the previous/latest Trace revision or version;
- how to avoid reintroducing duplicated inputs that can disagree;
- how HistoricalCorrection appends corrected public truth while leaving RfqCaseHistory immutable.

After the TraceRevision/API boundary is closed, continue to concrete historical-correction scenarios and construction strategies.
