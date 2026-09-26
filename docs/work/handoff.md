# RFQ Domain Rework — Handoff

This is the single active working handoff. It is non-canonical.

## Read first

Read in this order:

1. `../domain.md` — canonical Domain authority.
2. `README.md` — working-document roles and discussion method.
3. `topics/correction/case-history-and-trace.md` — retained rationale for the just-completed revision / restore / Trace foundation.
4. `topics/correction/correction-cases.md` — concrete historical-correction regression catalog.

Read `sessions/05-operational-history-restore-trace.md` if the rationale or boundary of the completed foundation unit is needed.

Do **not** start the next discussion from:

- `topics/correction/correction-history-model.md`;
- `sessions/03-rfqcase-correction-foundation.md`.

Those files preserve an earlier abstract correction exploration centered on whole-History replacement and a provisional `Supports(H_rep, H_true)` relation. Several assumptions from that exploration have now been replaced by the concrete RfqCaseRevision / TraceData model. Consult them only for historical rationale if a concrete question requires it.

When working material disagrees with `../domain.md`, the canonical Domain wins.

## Current status

Completed design units:

- positive-flow RfqCase;
- pre-publication RfqDraft;
- correction-foundation exploration;
- ContinuedAfterAway provenance refinement;
- operational revision chronology / Restore / durable TraceData.

The latest canonical foundation is:

    RfqCase
      = one complete valid business state

    RfqCaseRevision
      = one immutable operational occurrence

    RfqCaseHistory
      = retained revision chronology for one CaseId

    TraceData
      = durable self-contained business representation
        that may outlive operational history

Revision provenance is:

    RfqCaseTransition
    = Published(DraftId)
    | Applied(CaseCommand)
    | RestoredFrom(TargetVersion)

Version is Domain chronology and advances through `Next()` for every accepted operation after publication.

Operational Restore:

- may start from Open or Terminal current state;
- targets an earlier same-Case **Open** revision;
- creates one new revision at current.Version.Next();
- reuses the target revision's exact Case-local identities;
- creates one Superseded TraceData;
- does not reinstate earlier terminal outcomes;
- does not reverse external side effects.

Terminal operations create a new revision plus an Effective TraceData.

TraceData intentionally removes Case-local child IDs, materializes durable values, and may survive deletion of RfqCaseHistory.

## Current design target

Design **historical correction** on top of the canonical revision / Trace foundation.

The practical scope is initially one Case whose operational RfqCaseHistory is still retained.

Historical correction should answer:

> given investigation of a previously recorded Case, how do we construct a corrected durable business representation while reusing ordinary Domain semantics wherever they are still expressive?

The current working direction is:

    retained revision/history material
        ↓
    replay ordinary Case semantics where possible
        ↓
    if current RfqCase command language cannot express the intended truth,
    switch once into Trace-native correction semantics
        ↓
    produce corrected durable TraceData

Command-based correction is only required while operational history remains available. Once the source operational history has expired, arbitrary historical correction may be unsupported.

## Settled foundation to preserve

Unless a concrete historical-correction case exposes a contradiction:

- preserve the canonical positive-flow RfqCase state machine;
- preserve RfqCaseRevision chronology and monotone Version semantics;
- preserve Published / Applied / RestoredFrom provenance;
- preserve operational Restore as a distinct operation from historical correction;
- preserve Restore's earlier-Open-only target rule;
- preserve Case-local child identity inside operational history;
- preserve TraceData's removal of Case-local IDs and its durable/self-contained lifetime;
- preserve intentional Trace compression rather than expanding TraceData into the full operational log;
- preserve ActorId versus ContactOwnerId / QuoteOwnerId role distinctions;
- do not revive a broad Aggregate Root/loading assumption merely to make correction implementation convenient.

## Working direction for historical correction

A promising construction path is:

    initial Published revision's RfqCase
      -> replay selected/corrected ordinary CaseCommand transitions
      -> obtain valid ordinary business states
      -> optionally switch once to a Trace-side correction state
      -> apply EffectiveCommand values
      -> materialize corrected TraceData

Important:

- replay is a construction/validation mechanism, not automatically a claim that the corrected command sequence literally happened;
- a corrected command may change a recorded command input;
- commands may potentially be omitted or inserted if concrete correction cases require it;
- once Trace-native EffectiveCommand processing introduces facts not representable by RfqCase, processing should not return to RfqCase commands.

These are working directions, not yet canonical historical-correction semantics.

## First questions to resolve

Discuss these from concrete cases, not from the old Supports abstraction.

1. **Which revision path is the replay source?**  
   RfqCaseHistory may contain RestoredFrom edges and superseded operational paths. Define how a historical-correction target selects the relevant source path/terminal occurrence rather than assuming the physical v1..vn sequence is one linear replay program.

2. **What exactly is CaseCommand?**  
   Define the replayable accepted-command sum type and what each command must carry, especially generated Case-local IDs, ActorId, Timepoint, business dates, and other supplied values needed for deterministic reconstruction.

3. **What edits are allowed to the ordinary command program?**  
   Test value correction, command deletion, command insertion, reordered meaning, and corrections whose ordinary replay reaches a different valid state/history.

4. **When does ordinary replay stop being expressive enough?**  
   Use the concrete correction catalog to identify cases that cannot be represented by the current RfqCase positive-flow command language.

5. **What is the Trace-side intermediate state?**  
   Define the smallest state on which EffectiveCommand operates. Do not make it a generic mutable TraceData/set-path escape hatch.

6. **What is EffectiveCommand?**  
   Define business-semantic Trace-native transitions needed only when ordinary RfqCase replay cannot express the intended historical truth.

7. **What records does one Historical Correction produce?**  
   Determine how the previously effective representation becomes Superseded and how the corrected Effective TraceData is appended, including correction-of-correction.

8. **What validation relation is actually needed?**  
   Start from concrete business invariants and the correction cases. Introduce a Supports/adequacy relation only if the concrete model needs one; do not assume the earlier abstract relation is required.

9. **What remains outside the single-Case unit?**  
   Split/merge/reassociation across CaseIds, downstream reconciliation, authorization/approval workflow, and physical persistence schema should remain separate unless a concrete dependency forces them in.

## Concrete regression material

Use `topics/correction/correction-cases.md` to challenge each proposal.

In particular, make sure the model can eventually address at least:

- wrong/missing Hit or Away;
- wrong presented value or timing;
- wrong/missing pricing rounds;
- Terms/owner/date mistakes;
- a correction that changes history while leaving a similar current/final state;
- ValidUntil-dependent validity;
- correction-of-correction.

Cross-Case split/merge/reassociation cases remain valuable pressure tests but should not force the first single-Case correction model to solve distributed correction prematurely.

## Expected result of the next design unit

The next unit should finish with a concrete historical-correction construction model covering:

- replay source/path selection;
- CaseCommand shape;
- ordinary corrected replay;
- the boundary to Trace-native correction;
- EffectiveCommand and its state;
- Superseded + corrected Effective Trace production;
- correction-of-correction behavior;
- explicit remaining unsupported/deferred cases.

Do not start by formalizing a general equivalence relation or by designing UI/persistence APIs.
