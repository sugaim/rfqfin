# RFQ Domain Rework — Handoff

This is the single active working handoff. It is non-canonical.

## Read first

Read in this order:

1. `../domain.md` — canonical Domain authority.
2. `README.md` — working-document roles and discussion method.
3. `topics/correction/operational-correction.md` — current design target and unresolved operational-correction questions.

Read `sessions/04-continued-after-away-provenance.md` only if the rationale for the just-completed positive-flow refinement is needed.

Do **not** start this design unit by reading:

- `topics/correction/correction-history-model.md`;
- `topics/correction/correction-cases.md`;
- `sessions/03-rfqcase-correction-foundation.md`.

Those files belong to the earlier broad/abstract correction exploration and are intentionally deferred for the later historical-correction unit. They are not prerequisite context for the operational problem.

When working material disagrees with `../domain.md`, the canonical Domain wins.

## Current status

Completed design units:

- positive-flow RfqCase;
- pre-publication RfqDraft;
- correction-foundation exploration;
- ContinuedAfterAway provenance refinement.

The latest canonical refinement changed:

    ContinuedAfterAway(
        PreviousPricingEpisodeId,
        PresentationAwayOutcome
    )

`PresentationAwayOutcomeRef` is removed.

`QuotePresentation` remains immutable. The Away outcome remains a separate immutable business fact.

## Current design target

Design **operational correction** for an active/same-day RfqCase.

The concrete pattern is:

    prior valid Case state
      -> one or more mistaken ordinary operations
      -> mistake discovered
      -> return to a previously valid Case state
      -> continue ordinary positive-flow processing

A restore/jump to a previous valid Case representation is currently considered a plausible mechanism.

This unit must remain separate from later **historical correction**, where the goal is to construct the minimal persistent/effective business history that should be treated as true.

## Working direction, not yet settled

Candidate chronology:

    v10 = earlier valid state
    ...
    v20 = current mistaken path

    restore v10

    v21 = new current version with Case contents equivalent to v10

Then ordinary commands continue from v21.

The important working properties are:

- versions remain monotone; do not decrement back to v10;
- prior versions remain immutable;
- the mistaken path remains available as operational/audit chronology;
- the restored current Case again satisfies the normal positive-flow Domain invariants;
- restore does not automatically reverse external side effects.

Do not treat these as canonical until the next design unit confirms their semantics and ownership.

## First questions to resolve

Discuss these in order:

1. Is operational restore itself a Domain operation, or does Application/Persistence reconstruct a prior valid Case state as the next current version?
2. May restore target only the immediately previous version, or any earlier version of the same Case?
3. May a terminal Case be restored to an earlier Open state?
4. Does restore reuse the exact Case-local child identities from the selected version?
5. Does every accepted ordinary Domain command create a CaseVersion, and does one restore create exactly one new version?
6. Is any `RestoredFromVersion` / source-version metadata business meaning or merely audit/persistence metadata?
7. Is an explicit branch/finalization model needed for the abandoned v11..v20 path, or is chronological version history plus current pointer sufficient?
8. How should stale/concurrent restore requests and external side effects be handled at the Application/Persistence boundary?

Do not design historical-correction representation, Support/equivalence relations, cross-Case correction, or broad correction APIs in this unit unless a concrete dependency is discovered.

## Settled models to preserve

Unless operational correction exposes a concrete contradiction:

- preserve the canonical positive-flow RfqCase state machine;
- preserve immutable RfqTerms, PricingEpisode, Quote, and QuotePresentation occurrences;
- preserve Case-local child identities;
- preserve explicit PricingEpisode lineage through PreviousPricingEpisodeId;
- keep WorkingQuote outside RfqCase;
- keep actor authorization outside Domain validity;
- do not weaken ordinary positive-flow invariants merely to make restore easier.

## Expected result of the next session

The next design unit should finish with a compact operational-correction model covering:

- business meaning and naming;
- Domain/Application/Persistence boundary;
- legal restore targets;
- identity behavior;
- minimum CaseVersion semantics.

Only after that unit is complete should the work move to the separate historical-correction representation.
