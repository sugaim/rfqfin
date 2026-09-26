# RFQ Domain Rework — Handoff

This is the single active working handoff. It is non-canonical.

## Read first

Read in this order:

1. `../domain.md` — canonical Domain authority.
2. `README.md` — working-document roles and discussion method.
3. `topics/correction/case-history-and-trace.md` — current retained rationale for revision / Restore / Trace / replayable CaseOperation semantics.
4. `topics/correction/correction-cases.md` — concrete historical-correction regression catalog.

Read `sessions/06-first-class-case-operations.md` when the rationale for the current CaseOperation model is needed.

Read `sessions/05-operational-history-restore-trace.md` only when the earlier chronology/Restore/Trace checkpoint itself is useful.

Do **not** restart the next discussion from:

- `topics/correction/correction-history-model.md`;
- `sessions/03-rfqcase-correction-foundation.md`.

Those files preserve an earlier abstract correction exploration centered on whole-History replacement and a provisional `Supports(H_rep, H_true)` relation. Several assumptions from that exploration have now been replaced by the concrete RfqCaseRevision / CaseOperation / TraceData model.

When working material disagrees with `../domain.md`, the canonical Domain wins.

## Current status

Completed design units:

- positive-flow RfqCase;
- pre-publication RfqDraft;
- correction-foundation exploration;
- Away/repricing provenance refinement;
- operational revision chronology / Restore / durable TraceData;
- first-class RfqCase CaseOperation model and replayable operation payloads.

The current canonical foundation is:

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
    | Applied(CaseOperation)
    | RestoredFrom(TargetVersion)

## CaseOperation foundation

Ordinary Domain operations are first-class Domain Objects.

Conceptually:

    Apply :
        RfqCase x CaseOperation
        -> RfqCase

This is a partial function: every operation has explicit valid source-state shapes and invariants.

Current hierarchy:

    CaseOperation
    = Open(OpenOperation)
    | Terminal(TerminalOperation)

The operation definition is canonical. The state graph remains a state-centric reference view.

Application request models are separate from CaseOperation. Application resolves actor/time/date, generated IDs, and external context before constructing a fully resolved Domain Operation.

The same fully resolved CaseOperation is used for:

- live positive flow;
- Applied(CaseOperation) revision provenance;
- future retained-history replay/correction.

Values deterministically derivable from the source RfqCase are not duplicated merely for replay.

There is no separate ordinary replay-only CaseCommand model.

## Important positive-flow refinements now canonical

- ExpireQuote is removed; InvalidateQuote has Reason = Expired | Withdrawn.
- ExtendValidUntil is renamed ExtendValidity.
- ExtendValidity may occur after the old ValidUntil elapsed; current invariant is only NewValidUntil > CurrentValidUntil.
- ContinueAfterAway is replaced by RequestRepricingOnAway.
- PricingEpisodeOrigin for that operation is Away(...).
- PresentationAwayOutcome now includes AwayDate + RecordedBy / RecordedAt + Feedback?.
- Away RecordedBy / RecordedAt come from the operation that establishes the Away fact.
- Hit / old CloseAway are reorganized under TerminalOperation.CloseCase with CloseOutcome = Hit | Away | Unpresented.
- Away closure distinguishes AlreadyRecorded versus RecordNow only as operation-construction semantics, not as different business Away kinds.
- all accepted Case operations carry operation-specific business actor/time fields where relevant; no generic Stamp type is used.

## Terminal and Trace boundary

Terminal operations are:

    TerminalOperation
    = CloseCase
    | Cancel

The underlying business-state semantics can be reasoned about as:

    RfqCase x TerminalOperation
        -> Terminal RfqCase

but the public Domain API need not expose terminal-state creation separately from Trace generation.

Conceptually:

    ApplyTerminal(
        currentRevision,
        terminalOperation,
        terminalTraceContext
    ) -> TerminalResult {
         Revision,
         Trace
       }

The API boundary should preserve semantic production of terminal Revision + Trace together.

Trace state is:

    TraceState
    = Effective(TerminalTrace)
    | Superseded(TraceSnapshot, Reason)

    TraceSnapshot
    = Open(OpenTrace)
    | Terminal(TerminalTrace)

Trace remains intentionally compressed.

Detailed ActivityChange begins at first Presentation.

Current retained post-Presentation activities are:

- RepricingRequested;
- RepricingRequestedOnAway;
- QuoteOwnerChanged;
- PricingDateRolled;
- AssumedTradeDateChanged;
- ValidityExtended.

For activity facts retained in TraceData, actor/time is retained with the activity.

## Operational Restore

Operational Restore:

- may start from Open or Terminal current state;
- targets an earlier same-Case **Open** revision;
- creates one new revision at current.Version.Next();
- reuses the target revision's exact Case-local identities;
- creates one Superseded TraceData;
- does not reinstate earlier terminal outcomes;
- does not reverse external side effects.

A genuinely correct terminal Away/Close followed later by renewed customer interest is not automatically Restore.

## Current design target

Design **historical correction** on top of the canonical revision / CaseOperation / Trace foundation.

The practical scope is initially one Case whose operational RfqCaseHistory is still retained.

Historical correction should answer:

> given investigation of a previously recorded Case, how do we construct a corrected durable business representation while reusing ordinary Domain semantics wherever they remain expressive?

The ordinary replay language is now concrete:

    initial Published revision
      + Applied(CaseOperation)
      + RestoredFrom provenance

Replay is a construction/validation mechanism, not automatically a claim that the corrected operation sequence literally happened.

## Next question — start here

### 1. Reopen after a genuine terminal outcome

A newly identified positive-flow question should be resolved before historical-correction replay design:

> when a terminal outcome was genuinely correct at the time, can the same RfqCase later become Open again because business activity resumes?

This is **not** Operational Restore:

- Restore means the prior operational path/outcome was mistaken and an earlier valid Open revision is selected;
- Reopen would mean the terminal fact remains historically correct, but later business activity starts again.

Discuss at least these cases separately:

- `Closed(Presented(Away(...))) -> Open`: client genuinely went Away, then later returns and asks to resume/reprice;
- `Cancelled -> Open`: determine whether any cancellation meanings permit later reopening of the same Case, or whether cancellation means the Case identity itself should remain dead;
- `Closed(Presented(Hit(...)))`: do not assume symmetry with Away; Hit may already have booking/downstream effects and likely needs a separate business rule.

If Reopen is admitted, determine:

- whether the same CaseId continues or a new Case is required;
- which terminal facts remain durable/history-visible;
- what Open state/PricingEpisode is created on reopen;
- whether a new PricingEpisode is always required;
- whether Reopen is a CaseOperation and, if so, how the current `OpenOperation : Open -> Open` / `TerminalOperation : Open -> Terminal` hierarchy should change;
- how Reopen produces/updates durable TraceData;
- how Reopen differs from correction/Restore in revision provenance.

Do not modify Restore semantics merely to absorb genuine resumed business.

### 2. Replay source/path selection

After Reopen semantics are settled, continue historical-correction design.

RfqCaseHistory may contain:

- a physical v1..vn chronology;
- RestoredFrom edges;
- paths that were once effective and later superseded;
- terminal occurrences with already-materialized TraceData.

Do not assume that the physical revision sequence itself is one linear replay program.

Determine:

- what path/program is selected when correcting a particular historical business occurrence;
- how RestoredFrom is interpreted during replay;
- how current effective-state reconstruction differs, if at all, from reconstruction of historical paths needed for corrected durable Trace;
- how a correction targets a path that is no longer current but was historically effective.

Use concrete correction cases while deciding this.

## Questions after replay-path selection

3. **What edits are allowed to the ordinary CaseOperation program?**  
   Test value correction, operation deletion, insertion, and cases where corrected replay changes later applicability.

4. **When does ordinary replay stop being expressive enough?**  
   Use the correction catalog to find business truth that cannot be represented by valid RfqCase states/CaseOperation values.

5. **What is the Trace-side intermediate state?**  
   Define the smallest state needed by any Trace-native correction language.

6. **What is EffectiveCommand, if still needed?**  
   Define business-semantic Trace-native transitions only for cases ordinary replay cannot express.

7. **What records does one Historical Correction produce?**  
   Determine Superseded + corrected Effective Trace behavior, including correction-of-correction.

8. **What validation/adequacy relation is actually needed?**  
   Introduce one only if concrete cases require it; do not restart from Supports(H_rep,H_true).

9. **What remains outside the single-Case unit?**  
   Split/merge/reassociation across CaseIds, downstream reconciliation, authorization/approval workflow, and physical persistence schema remain separate unless a concrete dependency forces them in.

## Concrete regression material

Use `topics/correction/correction-cases.md`.

In particular test:

- wrong/missing Hit or Away;
- wrong presented value or timing;
- wrong/missing pricing rounds;
- Terms/owner/date mistakes;
- correction that changes history while leaving a similar current/final state;
- ValidUntil-dependent validity;
- correction-of-correction.

Cross-Case split/merge/reassociation remains deferred.

## Working discipline

- preserve canonical positive-flow semantics unless a concrete correction case exposes a contradiction;
- do not reintroduce CaseCommand;
- do not turn TraceData into a complete operational log;
- keep Domain validity distinct from Application authorization and Persistence mechanics;
- do not expose internal transition/Trace factorization as public API merely because it is useful for reasoning;
- do not start by formalizing a general equivalence relation;
- when the next coherent design unit is complete, update canonical docs first, then write a new session record, then update this handoff.
