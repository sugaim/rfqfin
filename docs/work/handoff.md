# RFQ Domain Rework — Handoff

This is the single active working handoff. It is non-canonical.

## Why this handoff exists

The previous discussion made useful progress, but late in the session the conversational context became mixed: canonical facts, already-settled changes, and new proposals were repeatedly conflated.

Therefore:

- treat canonical `../domain.md` as authoritative;
- treat the pending Trace simplification / TraceRevision conclusions as working decisions to be **reconfirmed in a fresh session**;
- do not copy those pending conclusions mechanically into canonical docs;
- after reconfirmation, update canonical docs before treating them as settled.

## Read first

Read in this order:

1. `../domain.md` — canonical Domain authority.
2. `README.md` — working-document roles and discussion method.
3. `topics/correction/case-history-and-trace.md` — active working summary, including the pending Trace simplification / TraceRevision checkpoint.
4. `sessions/08-trace-api-and-revision-checkpoint.md` — why the current API shape is canonical and why the remaining Trace work is being rechecked.
5. `topics/correction/correction-cases.md` — concrete correction regression catalog when correction scenarios are discussed.

Use Sessions 05–07 only when earlier rationale is needed.

Do not restart from the old abstract `Supports(H_rep, H_true)` correction model.

## Canonical foundation already settled

The following is already incorporated into `domain.md`.

### Ordinary business operations

    Apply :
        RfqCase x CaseOperation
        -> RfqCase

    CaseOperation
    = Open(OpenOperation)
    | Terminal(TerminalOperation)
    | Reopen(ReopenOperation)

Reopen is one flat operation:

    ReopenOperation
    - NewPricingEpisodeId
    - ReopenDate
    - NewAssumedTradeDate
    - ReopenedBy
    - ReopenedAt

The source Terminal state determines reopen provenance and target Open state.

### Operational revision chronology

    RfqCaseRevision
    - CaseId
    - Version : CaseVersionNumber
    - Case
    - Transition

    CaseVersionNumber
    - Value
    - New()
    - Next()
    - Prev()

    RfqCaseTransition
    = Published(DraftId)
    | Applied(CaseOperation)
    | RestoredFrom(TargetVersion)

Restore is not CaseOperation.

    RestoreOperation
    - TargetVersion : CaseVersionNumber

### Trace-producing revision-level API

RfqCaseHistory is the source of current state and retained chronology.

    TraceContext
    - RecordedBy
    - RecordedAt
    - RecordedBusinessDate

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

Do not reintroduce separate current Case/current revision inputs unless a concrete contradiction requires it; History is deliberately the source of truth.

Restore still materializes the abandoned effective path into the durable TraceRecord, while the new RfqCaseRevision selects the target Open revision.

## Pending working decisions — reconfirm before canonical update

The previous session reached the following working conclusions, but they are intentionally not yet canonical because of the context-mixing issue.

Re-read the full working summary in `topics/correction/case-history-and-trace.md`, then explicitly confirm or revise these points before editing `domain.md`.

Current checkpoint includes:

- simplify public/business Trace to a small set of business milestones rather than operational changes;
- candidate TraceActivity set: TermsAmended, Presented, Away, InternalRepricingRequested, Hit, Closed, Cancelled, Reopened;
- Restore is not a TraceActivity;
- Presented becomes a self-contained snapshot;
- public Trace does not expose PresentationId;
- Hit/Away and Closed remain separate facts;
- InitialContext / EndContext remain and include Terms, ContactOwnerId, QuoteOwnerId, PricingDate, AssumedTradeDate;
- TraceRecord becomes a versioned Domain concept, tentatively TraceRevision;
- TraceRevision chronology is independent from RfqCaseRevision chronology;
- outer reason is named Trigger rather than Kind;
- Restore / HistoricalCorrection triggers carry actor/time and CorrectionReason;
- initial CorrectionReason taxonomy is DataError | OperationalError | Other plus Note.

These are strong working decisions, but the next session must validate them once more against canonical Domain semantics before incorporation.

## Start here

First finish the TraceRevision/version/API boundary.

Questions to answer:

1. What is TraceVersionNumber / TraceRevision identity and sequence semantics?
2. How does a Trace-producing operation obtain the previous/latest Trace revision or version?
3. How do we preserve the History-as-single-source-of-truth principle and avoid inconsistent duplicated inputs?
4. What is the final RevisionTraceResult after TraceRecord -> TraceRevision?
5. What is the HistoricalCorrection API when RfqCaseHistory remains immutable and correction appends a new corrected Trace revision?

Do not move to broad historical-correction machinery until this boundary is coherent.

## After TraceRevision/API is closed

Then test representative correction scenarios:

- wrong value;
- extra activity;
- missing activity;
- wrong activity kind;
- correction that changes later business history;
- business truth not representable by the current RfqCase / CaseOperation model.

Only then choose among:

- ordinary CaseOperation replay;
- hybrid replay + correction-native construction;
- Trace-native construction;
- a minimal ephemeral TraceProjectionState, if concrete cases require it.

## Working discipline

- canonical first;
- distinguish canonical facts from pending working decisions;
- because this handoff exists due to context mixing, re-confirm pending decisions before canonical edits;
- preserve settled positive-flow semantics unless a concrete contradiction appears;
- keep RfqCaseHistory immutable during historical correction unless a new requirement explicitly overturns that premise;
- do not expand public Trace merely to reproduce operational History;
- discuss one coherent question at a time;
- when the TraceRevision/API unit is complete, update canonical docs first, then session/topic/handoff.
