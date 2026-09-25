# RFQ Domain Rework — Handoff

This is the single active working handoff. It is non-canonical.

## Read first

Read in this order:

1. `../domain.md` — canonical Domain authority.
2. `README.md` — working-document roles and discussion method.
3. `topics/correction/correction-history-model.md` — current correction semantic model and exact discussion frontier.
4. `topics/correction/correction-cases.md` — concrete correction cases used as regression material.
5. `sessions/03-rfqcase-correction-foundation.md` — rationale and major refinements from the completed correction-foundation unit.

Read `sessions/01-rfqcase-positive-flow.md` and `sessions/02-rfqdraft.md` only when their rationale is needed.

Topic/session material does not override `../domain.md`.

## Current status

Completed design units:

- positive-flow RfqCase;
- pre-publication RfqDraft;
- correction foundation / history-model exploration.

The first two are incorporated into canonical `../domain.md`.

The correction foundation is intentionally **not yet canonical**. It reduced the problem to a narrower semantic question and is maintained under `topics/correction/`.

The canonical positive-flow model remains unchanged.

## Settled models to treat as fixed

Treat the current positive-flow RfqCase and RfqDraft models as fixed unless correction exposes a **concrete business contradiction**.

In particular:

- Draft must not be reintroduced into RfqCase.State;
- WorkingQuote remains outside RfqCase Domain;
- actor authorization does not define Domain operation identity;
- PricingDate and AssumedTradeDate remain distinct;
- full historical collections are not assumed to be loaded into the current RfqCase aggregate;
- correction design must not silently weaken existing positive-flow invariants;
- exploratory Entity/Value Object questions from correction are not permission to redesign current child identities without a concrete Support requirement.

## Correction foundation now established

The case survey showed that correction cannot safely be reduced to generic undo or reversal.

Current working model:

    S = business State
    C = business operation / command
    δ : S × C ⇀ S

    H =
    {
      (s0, ..., sn)
      |
      each adjacent pair is connected by some permitted command
    }

History is currently modeled as a **finite sequence of valid business States**. Commands witness legal transitions but are not themselves part of History.

Correction aims to make an acceptable corrected History effective rather than to discover a unique inverse command over the recorded History.

A provisional adequacy relation was introduced:

    Supports : H × H -> Bool

with:

    Supports(H_rep, H_true)

meaning approximately that `H_rep` sufficiently represents the business meaning that must be preserved from `H_true`.

Both the **name** and the **formal shape** are provisional. The next discussion should not assume that `Supports` is the final abstraction.

## Revision direction

Whole-History revision/replacement is a strong implementation direction because it provides:

- immutable prior representations;
- straightforward correction-of-correction;
- weaker coupling between correction machinery and future Domain additions;
- a natural logical unit because an individual Case history is finite at any point in time.

However:

- Revision is **not currently an RfqCase business transition**;
- revision/version identity is presently treated as Application/Persistence machinery;
- physical storage is unresolved and need not copy every historical object naïvely.

The useful distinction borrowed from bitemporal reasoning is:

- business History: what should now be treated as having happened;
- revision/audit history: what representation the system held over time.

Do not collapse those dimensions.

## Command replay/planning idea

Command-based reconstruction remains potentially useful.

Application may eventually:

1. obtain the business facts/constraints that correction must express;
2. search for or construct ordinary business commands;
3. replay them through the positive-flow Domain;
4. produce a valid candidate History;
5. verify the candidate using `Supports`;
6. surface only ambiguous business decisions for manual resolution.

Such a command sequence is a construction witness, not automatically the literal historical command sequence.

This is an Application possibility, not a selected implementation.

## Next design target

Before fixing the adequacy relation, organize the **preservation requirements** implied by the current RfqCase Domain model.

The immediate question is:

> For each business concept represented by RfqCase, what must a corrected History preserve, and for what purpose?

Use `topics/correction/correction-cases.md` as regression material.

At minimum, keep these requirement dimensions separate:

- customer-facing business truth — e.g. what was shown, when, and its Hit/Away result;
- internal business/process meaning — e.g. whether ContinueAfterAway, repricing, ownership/Terms changes, or PricingEpisode.Origin matter historically;
- current operational truth — some correction classes may need only the correct current state;
- referential continuity — IDs may need preservation/mapping because unaffected or downstream data refer to them even when the IDs are not business-equivalence facts;
- audit/revision history — prior recorded representations and correction metadata are a separate dimension from corrected business History.

After these are understood, decide whether the adequacy relation should remain `Supports(H_rep, H_true)`, be renamed (for example toward preservation), or be reformulated in requirement-oriented terms such as `Satisfies(H_rep, Requirements(H_true))`.

Do **not** jump yet to:

- a full correction command/API catalog;
- exact revision schema;
- UI/workflow;
- unrestricted graph mutation;
- event sourcing;
- multi-Case implementation mechanics.

## Important frontier from the previous discussion

Observation-based adequacy was only explored, not settled.

Examples discussed include:

- whether a Presentation occurred;
- what was presented;
- when it was presented;
- Hit/Away result and timing;
- ordering;
- internal process events such as ContinueAfterAway;
- values;
- internal identities/references.

Two distinctions should be preserved in the next discussion:

1. internal Domain identity and business-history equivalence are not automatically the same thing;
2. business equivalence and referential continuity are not automatically the same thing.

For example, from a customer-facing perspective a Quote may matter only by its value/proposition, while an unchanged downstream record may still require the old QuoteId to remain resolvable or mapped.

Do not treat any particular Presentation/Quote/Outcome observation or preservation rule as decided.

## Intentionally deferred

Unless the Support discussion itself requires them, defer:

- exact CancellationReason taxonomy;
- final Presented-Away Case-level disposition taxonomy;
- typed analytics/Feedback taxonomy;
- foreign-market settlement context;
- settlement amount/currency/FX semantics;
- first-class Case/Draft lineage;
- exact audit/event schema;
- broad API/DTO/frontend migration;
- full Application authorization/use-case design;
- exact revision persistence;
- downstream propagation/reconciliation;
- cross-Case split/merge/reassociation implementation.

## Working approach for the next session

- Start from the current model, not from the exploratory chat history.
- Start by reviewing the current Domain concepts one at a time and asking what preservation requirement, if any, each one creates.
- Keep customer-facing truth, internal process meaning, current-state sufficiency, referential continuity, and audit/revision history separate.
- Test every proposed requirement against concrete cases.
- Distinguish accepted rule, hypothesis, and example.
- Preserve positive-flow invariants.
- If a Support requirement creates a concrete contradiction with the canonical Domain model, stop and discuss that contradiction before editing `domain.md`.
- Do not infer business truth merely because a command sequence is legal.

## Why start a fresh session now

The correction-foundation discussion deliberately explored many directions: concrete cases, generic reversal, revision shapes, bitemporal implications, identity, observations, and command replay.

That exploration was useful, but carrying the conversational path forward would risk anchoring the next discussion on abandoned or provisional ideas.

The reusable context has therefore been normalized into:

- `topics/correction/correction-history-model.md`;
- `topics/correction/correction-cases.md`;
- `sessions/03-rfqcase-correction-foundation.md`.

The next session should use those files rather than rely on the prior chat transcript.
