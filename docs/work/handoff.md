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

Introduce:

    Supports : H × H -> Bool

with:

    Supports(H_rep, H_true)

meaning that `H_rep` sufficiently represents the business meaning that must be preserved from `H_true`.

No concrete definition of `Supports` has yet been accepted.

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

Continue from:

    Supports(H_rep, H_true)

The immediate question is:

> What business observations or requirements make one valid History a sufficient representation of another for correction purposes?

Use `topics/correction/correction-cases.md` as the regression catalog.

Do **not** jump yet to:

- a full correction command/API catalog;
- exact revision schema;
- UI/workflow;
- unrestricted graph mutation;
- event sourcing;
- multi-Case implementation mechanics.

## Important frontier from the previous discussion

Observation-based Support was only explored, not settled.

Examples discussed include:

- whether a Presentation occurred;
- what was presented;
- when it was presented;
- Hit/Away result and timing;
- ordering;
- values;
- internal identities/references.

A useful but still provisional distinction is:

> internal Domain identity and business-history equivalence are not automatically the same thing.

For example, a customer-facing business fact may be “price X was shown at time T and accepted,” while the internal QuoteId/PresentationId may or may not belong to the equivalence relation.

Do not treat any particular Presentation/Quote/Outcome observation rule as decided.

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
- Take one coherent Support question at a time.
- Test every proposed rule against concrete cases.
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
