# Design Session 03 — RfqCase Correction Foundation

## Goal

Establish a semantic foundation for RfqCase correction before designing concrete correction commands.

The session began from the correction target in `../handoff.md`: correction/reversal/historical amendment should be derived from concrete business cases rather than from a generic undo mechanism.

This session intentionally does **not** complete the full correction design. It completes the exploratory/foundation unit needed so the next discussion can focus narrowly on business-history equivalence.

Current working material:

- `../topics/correction/correction-history-model.md` — semantic model and current frontier;
- `../topics/correction/correction-cases.md` — concrete regression/test-case catalog.

These working-topic documents are non-canonical.

## Work performed

A broad set of correction cases was examined rather than starting from a preferred implementation.

The cases included:

- erroneous and amended Hit/Away outcomes;
- wrong Quote/Presentation values and timing;
- Terms mistakes with different causal meanings;
- omitted/extra PricingEpisodes;
- owner/date/root-field corrections;
- expiry/ValidUntil interactions;
- historical-only corrections whose current state may look unchanged;
- correction-of-correction;
- upstream/master-data errors;
- split, merge, duplicate, and cross-Case reassociation problems.

The normalized case set is retained in `../topics/correction/correction-cases.md` so future rules can be regression-tested against the same concrete pressures.

## Major conclusion from the case survey

Correction is not reliably modeled as:

    current state
      + inverse/revert operation
      = corrected state

The cases showed that correction may need to:

- remove an earlier fact while preserving later valid facts;
- re-associate later facts after removing an intermediate state;
- insert a missing historical state;
- replace a terminal suffix with a longer open or later-terminal history;
- correct an earlier correction without reverting the whole previous correction;
- distinguish histories that end in the same current state;
- coordinate multiple Cases.

Therefore no generic undo/revert mechanism was accepted as the semantic foundation.

Explicit typed correction operations may still be useful later for narrow cases.

## External research

Several external models were reviewed for implications rather than adopted directly:

- system-versioned temporal persistence;
- bitemporal history;
- FIX Trade Cancel/Correct;
- workflow process-instance modification;
- Step Functions redrive;
- process-mining replay/alignment;
- classical planning;
- event-sourcing compensation/replay;
- model finding and labeled-transition/process-algebra ideas.

The most important implications retained are:

- current effective truth and immutable prior representations can coexist;
- business history and the history of system beliefs/recordings are separate dimensions;
- typed correction works well for narrowly standardized facts but does not automatically generalize to arbitrary Case history;
- replay/alignment/planning can help construct a valid target without proving what historically happened.

Details and references are retained in `../topics/correction/correction-history-model.md`.

## Revision exploration

Several revision shapes were considered.

### Per-object / per-entity revision

Revisioning individual Terms, Episodes, Quotes, Presentations, etc. preserves strong immutability but creates substantial graph/reference complexity.

It was not selected as the correction foundation.

### Phase/local revision

Revisioning only an affected phase/suffix was considered as a way to reduce whole-history replacement.

Boundary-crossing corrections and corrections that change the interpretation of downstream facts make the dependency closure difficult to define cleanly.

This may still be a persistence optimization, but it was not accepted as the semantic unit.

### Whole-History revision

A stronger candidate emerged:

    Revision 1 -> History H1
    Revision 2 -> History H2
    Revision 3 -> History H3

Advantages identified:

- immutable prior representations remain available;
- correction-of-correction becomes straightforward;
- the newest revision can represent the best history now believed rather than reversing the previous correction procedurally;
- future Domain additions need not automatically require a mirrored family of correction operations;
- an individual Case history is finite at any observation point, which makes a complete logical History a tractable correction unit.

Physical storage is intentionally undecided. Whole-History is a logical model and does not require naïvely duplicating every persisted object.

## Important refinement: Revision is not currently a Domain concept

The discussion initially treated Revision as a possible Domain abstraction.

That was weakened.

Current direction:

- **business History** describes what should be treated as having happened;
- **revision/audit history** describes which representation the system held over time;
- a RevisionId/version can therefore remain Application/Persistence machinery;
- replacing the effective History need not itself be an RfqCase business transition.

A future concrete requirement could justify a first-class correction/amendment Domain fact, but none has yet been established.

## Transition-system foundation

The correction discussion introduced a small metamodel.

Let:

    S = business State
    C = business operation / command
    δ : S × C ⇀ S

`δ` is partial and executes business-permitted transitions.

`C` is not restricted to the exact endpoints currently exposed by the Application.

For RfqCase, `S` means a complete effective business state sufficient for future Domain behavior, not merely the enum-like state variant.

### Produced-fact output was considered and dropped

An earlier formulation considered:

    δ(S, C) = (S', F)

where `F` represented newly established Domain facts.

This was not retained because current RfqCase semantics often preserve transition meaning in the resulting State itself. `PricingEpisode.Origin` is the clearest example.

If a concrete future case demonstrates business-significant transition meaning that cannot reasonably survive in State, an explicit transition observation/fact can be reconsidered.

## History definition

History was separated from command execution.

Current working definition:

    H =
    {
      (s0, ..., sn)
      |
      for every i < n,
      there exists ci in C such that
      δ(si, ci) = s(i+1)
    }

So:

> History is a finite sequence of valid business States.

Commands witness legal adjacency but are not themselves part of History.

This avoids making correction truth depend on reconstructing the exact command sequence.

It also means that if two histories contain pairwise equal States, hidden differences in commands do not distinguish those Histories under the current model. If such a distinction is business-significant, the model must preserve it in State or explicitly add transition-level meaning.

## Correction target and Support

The discussion then separated:

- `H_recorded` — the currently effective recorded History;
- `H_true` — the business History investigation says should be represented;
- `H_corrected` — a valid candidate replacement.

The semantic target is not defined by its relationship to the immediately previous recorded revision.

Instead:

    H_corrected ∈ H

and:

    Supports(H_corrected, H_true)

where:

    Supports : H × H -> Bool

means that the candidate is a sufficient business representation of the truth that must be preserved.

No symmetry, transitivity, equivalence, preorder, or concrete observation implementation has been accepted.

This is the main frontier left for the next session.

## Why History equality and current-state equality are both insufficient defaults

Exact History equality may be stronger than correction needs if some internal intermediate states do not carry business meaning that must be preserved.

Current/final-state equality is demonstrably too weak: several concrete cases reach the same final lifecycle shape through materially different presentations, outcomes, or pricing histories.

`Supports` is intended to describe the required strength between those extremes.

## Observation exploration

The session briefly tested possible observations such as:

- Presentation occurrence;
- what was presented;
- when it was presented;
- Hit/Away outcomes and timing;
- ordering;
- Quote/Terms values;
- identity/reference relationships.

One useful distinction emerged:

> internal Domain identity and business-history equivalence are not automatically the same thing.

For example, in a customer-facing interpretation it may matter that a certain price was shown at a certain time and accepted, while the internal QuoteId or PresentationId may or may not matter.

No concrete preservation rules were accepted.

In particular, do not treat the exploratory discussion of Presentation, Quote, Outcome, or customer-facing observations as settled design.

## Entity / Value Object tangent

Correction pressure raised questions about whether some current child identities are business-significant or primarily representational.

Presentation appears naturally occurrence-like, while Quote and PricingEpisode prompted more nuanced questions.

No Entity/Value Object reclassification was accepted.

Do not reopen the settled positive-flow model solely from this exploratory tangent. Revisit identity only if a concrete Support requirement exposes a contradiction.

## Application-side replay/planning idea

Command-based correction was not rejected.

Instead its role was reframed.

A future Application helper may:

1. receive business constraints describing the intended truth;
2. search for or construct ordinary business commands;
3. replay those commands through `δ`;
4. obtain a valid candidate History;
5. validate that candidate against `Supports`;
6. ask for human business judgment only where truth remains ambiguous.

The generated command sequence is a **construction witness**, not necessarily the actual historical command sequence.

This may substantially reduce the operational burden of correction while reusing positive-flow Domain invariants.

## Domain / Application / Persistence boundary at session end

Current working separation:

### Domain / business semantics

- business States and permitted transitions;
- valid History;
- eventually, the semantic adequacy relation expressed by `Supports` or an equivalent formulation.

### Application

Potentially:

- correction workflow and authorization;
- gathering business intent/evidence;
- command search/replay/planning;
- choosing which candidate to propose;
- downstream orchestration.

### Persistence / audit

Potentially:

- immutable prior history representations;
- revision/version identity;
- effective-version pointer;
- audit actor/time;
- physical structural sharing/delta storage.

These boundaries remain working direction until the correction design is complete.

## Canonical documentation

No canonical Domain changes were made in this session.

That is intentional.

The positive-flow RfqCase model remains the authority in `../../domain.md`. The correction foundation is being kept under `work/topics/correction/` until the semantic model is concrete enough to justify canonical changes.

## Why this design unit ends here

The exploratory problem is now sufficiently reduced.

The session began with a broad question:

> What should correction look like?

It ends with a narrower one:

> What makes one valid History a sufficient business representation of another?

The case survey, external research, revision discussion, and transition/history model now provide enough context to answer that narrower question without carrying the exploratory conversation forward.

The next design unit should therefore start from:

    Supports(H_rep, H_true)

and test candidate Support rules against `../topics/correction/correction-cases.md`.

Concrete correction commands, persistence schema, and full Application workflow should remain downstream until this semantic relation is understood.
