# Correction History Model

## Status

This is a non-canonical working model for the RfqCase correction design unit.

Its purpose is to let a later discussion rebuild the correction context quickly without replaying the exploratory conversation.

Read in this order:

1. `../../../domain.md` — canonical positive-flow Domain model;
2. `../../handoff.md` — active design target and boundaries;
3. this file — current correction semantics;
4. `correction-cases.md` — concrete cases used to challenge the model.

This file does **not** define a correction API and does **not** override `domain.md`.

## Goal

The active design goal is the one stated in `../../handoff.md`:

> design RfqCase correction / reversal / historical amendment from concrete business cases, while preserving the settled positive-flow model unless correction exposes a concrete contradiction.

The immediate sub-goal is narrower:

> define what makes a corrected business History an acceptable representation of the business history that should have happened.

That question must be answered before choosing concrete correction commands, revision persistence, or UI workflow.

## Why correction needs a separate semantic model

A large correction-case survey was performed before committing to an abstraction. The normalized catalog is in `correction-cases.md`.

The cases cover:

- wrong or missing Hit/Away outcomes;
- wrong Quote/Presentation values or timing;
- omitted or extra PricingEpisodes;
- Terms and ownership mistakes;
- corrections that change only historical facts while leaving current state similar;
- correction-of-correction;
- upstream-data mistakes;
- one Case that should have been two;
- two Cases whose histories were mixed or duplicated.

The main conclusion from that survey is not a concrete operation set. It is that correction is too varied to model safely as a generic inverse of the last operation.

Several patterns recur:

- the wrong part may be historical rather than current;
- a valid later fact may need to survive while an earlier fact is removed;
- removing an intermediate state may require later facts to be re-associated;
- the final state may be identical while the business history is materially different;
- a previous correction may itself be only partly wrong;
- changing one historical fact can invalidate dependent facts;
- sometimes the intended truth is underdetermined until business investigation resolves it.

A second pressure is maintainability. A correction subsystem that mirrors every Domain field and operation with dedicated amend/reverse variants would tend to grow in lockstep with the Domain and could become an obstacle to future Domain changes.

The working model therefore starts from **the desired corrected history**, not from a closed catalog of correction operations.

## External approaches and their implications

External approaches were reviewed for ideas, not selected as architectures.

### Temporal / system-versioned persistence

Temporal tables show that current effective truth and immutable prior representations can coexist.

Implication:

> retaining prior representations for audit does not require correction itself to be a Domain transition.

Reference:

- https://learn.microsoft.com/en-us/sql/relational-databases/tables/temporal/overview

### Bitemporal modeling

Bitemporal modeling distinguishes, in different terminology, between:

- what should be treated as true in the business world; and
- what the system recorded or believed at different transaction times.

Implication:

> **business History** and **revision/audit history** are different dimensions.

For correction, these answer different questions:

- Business History: “Given what is now known, what should we treat as having happened?”
- Revision history: “What representation did the system hold at each point in time?”

The current model borrows this distinction without committing to a full bitemporal database design.

Reference:

- https://www.martinfowler.com/articles/bitemporal-history.html

### Explicit cancel/correct protocols

FIX-style cancel/correct chains show that explicit typed correction works well where the corrected object and correction semantics are narrowly standardized.

Implication:

> specific correction facts may eventually justify typed correction operations, but this does not solve arbitrary RfqCase history correction by itself.

References:

- https://www.fixtrading.org/online-specification/trade-appendix/
- https://www.fixtrading.org/wp-content/uploads/download-manager-files/FIX-Latest-Specification-Trade.pdf

### Workflow repair, redrive, alignment, and planning

Camunda process modification, AWS Step Functions redrive, process-mining alignment, and classical planning all provide variants of:

- preserve a valid prefix;
- move/reconstruct execution;
- compare an observed trace with a valid process;
- search for a legal action sequence reaching a target.

Implication:

> Domain commands may be useful **construction and validation tools** for correction even if the command sequence is not itself the historical truth.

References:

- https://docs.camunda.org/javadoc/camunda-bpm-platform/7.24-SNAPSHOT/org/camunda/bpm/engine/runtime/ProcessInstanceModificationBuilder.html
- https://docs.aws.amazon.com/step-functions/latest/dg/redrive-executions.html
- https://github.com/process-intelligence-solutions/pm4py/blob/release/docs/source/api.rst
- https://www.fast-downward.org/latest/documentation/planner-usage/

Event sourcing, model finding, and labeled-transition/process-algebra ideas were also considered. No decision has been made to adopt any of them.

## Revision: useful implementation direction, not the semantic definition

Whole-History revision became attractive during the case analysis.

Conceptually:

    Revision 1 -> H1
    Revision 2 -> H2
    Revision 3 -> H3

Each revision can retain an immutable complete logical representation of the Case history understood at that point.

This is attractive because:

- prior representations remain available for audit;
- correction-of-correction does not require “undoing the undo”;
- a new revision can simply express the best History now believed;
- correction machinery need not mirror every future Domain field;
- the logical unit is a finite RfqCase History.

The last point is specifically useful here: an RFQ Case has a finite history at any observation point. That makes a whole-History logical value tractable even if the physical persistence layer later stores shared structure or deltas rather than copying every object.

However:

> **Revision is not currently part of the correction semantics.**

A revision identifier/version is presently best understood as an Application/Persistence coordinate over immutable representations.

The semantic correction question is instead:

> what History should now be effective?

This separation is deliberate.

## Core semantic model

The model below is a working metamodel. It is intended to be general enough to reason about correction without forcing the current in-memory RfqCase shape to carry full history.

### State

Let:

    S = set/type of business States

For RfqCase, a State means the complete effective business state needed to determine valid future business behavior, not merely the enum-like `RfqCase.State` variant.

### Business operation / command

Let:

    C = set/type of business operations

and:

    δ : S × C ⇀ S

where `δ` is partial because not every operation is permitted from every State.

Interpretation:

> `δ` executes a business-permitted state transition.

`C` should not be confused with the exact set of current Application endpoints. The Application may expose only some business operations directly.

### History

Let `H` be the set of finite valid State sequences:

    H =
    {
      (s0, ..., sn)
      |
      for every i < n,
      there exists ci in C such that
      δ(si, ci) = s(i+1)
    }

Therefore:

> **History is a finite sequence of business States connected by permitted business operations.**

Commands are not themselves part of History under the current model.

They witness that adjacent States can be connected legally and may help construct/validate Histories.

If a transition distinction is business-significant and must survive historically, the current preference is to represent that distinction in State where reasonable. `PricingEpisode.Origin` is an existing example.

If a concrete case later proves that some business-significant transition meaning cannot reasonably be represented or recovered from States, the model may need an explicit transition observation/fact. That is not assumed yet.

## Correction target

Let:

    H_recorded ∈ H

be the currently effective recorded History, and let:

    H_true ∈ H

stand for the business History that investigation says should be represented.

Current scope assumes that the intended business truth is expressible in the current Domain vocabulary. If it is not, that is first a Domain-model enhancement problem.

A correction candidate is:

    H_corrected ∈ H

The semantic target is **not** defined as an inverse operation over `H_recorded`.

Instead, correction aims to produce a valid History that sufficiently represents `H_true`.

The recorded History still matters for:

- identifying what changed;
- authorization/workflow;
- audit/provenance;
- downstream delta handling;
- side-effect reconciliation.

But it is not the sole semantic criterion for correctness.

## Support relation

Introduce an intentionally abstract relation:

    Supports : H × H -> Bool

with direction:

    Supports(H_rep, H_true)

meaning:

> `H_rep` is a sufficient business representation of the meaning that must be preserved from `H_true`.

A corrected History should satisfy:

    H_corrected ∈ H

and:

    Supports(H_corrected, H_true)

No symmetry, transitivity, equivalence, preorder, or observation-based implementation is assumed yet.

### Why Support is needed

Exact History equality may be too strong.

The true business process may contain internal intermediate states that do not need to be retained to preserve the business meaning relevant to correction.

Final-state equality is clearly too weak.

The case catalog contains examples where the same final lifecycle state hides different:

- customer presentations;
- outcomes;
- pricing rounds;
- ownership changes;
- Terms histories.

`Supports` is the placeholder for the business-equivalence strength required between those extremes.

## Observation discussion: current frontier, not a decision

The latest discussion began testing whether `Supports` could be defined from business observations extracted from History.

Candidate observation categories included:

- existence/absence of a customer interaction;
- what was presented;
- when it was presented;
- Hit/Away result and timing;
- ordering;
- values;
- identity/reference relationships where identity itself is business-significant.

One useful emerging distinction is:

> internal Domain identity and business-history equivalence are not automatically the same thing.

For example, in a customer-facing comparison it may matter that a particular price was shown at a particular time and then accepted, while the internal `QuoteId` or `PresentationId` used to represent that interaction may or may not matter to business equivalence.

This is **not settled**. No preservation rule for Quote, Presentation, Outcome, Terms, Episode, or IDs has yet been accepted.

The next discussion should resume here.

## Command replay / planning as an Application helper

Although command sequences are not History, they may be highly useful operationally.

A possible future correction workflow is:

    business establishes intended facts
        ↓
    derive target constraints / observations
        ↓
    search for or construct ordinary Domain command sequence
        ↓
    replay through δ
        ↓
    obtain valid candidate H_corrected
        ↓
    verify Supports(H_corrected, H_true)
        ↓
    make candidate effective

This could reduce manual correction work because the Application could use the ordinary positive-flow Domain to construct much of the candidate History and surface only ambiguous business decisions.

The command sequence would be a **construction witness**, not a claim that those commands literally occurred historically.

This distinction matters: legality can help construct a valid candidate, but it cannot infer business truth.

## Current working conclusions

Treat these as the current discussion baseline, not canonical Domain law:

- Correction is not modeled as generic undo.
- History is a finite valid State sequence.
- Business operations connect States but are not themselves currently part of History.
- Correction is about making the intended business History effective, not finding a unique inverse command.
- Whole-History revision/replacement is a strong implementation direction.
- Revision/version identity is currently Application/Persistence machinery, not an RfqCase business transition.
- Previous immutable revisions are useful for audit and correction-of-correction.
- The old recorded History matters operationally but does not define semantic adequacy.
- `Supports(H_rep, H_true)` is the current abstraction for semantic adequacy.
- Command replay/search may be useful as a construction/validation helper.

## Intentionally open

Do not silently decide these while using this note:

- the concrete definition of `Supports`;
- whether Support is defined by observations, direct predicates, or another structure;
- which business facts must be preserved exactly;
- which internal IDs are business-significant versus representational;
- whether transition-only business facts are required;
- exact revision storage/version schema;
- correction authorization/approval workflow;
- downstream propagation/reconciliation;
- multi-Case split/merge/reassociation;
- interaction between Domain evolution and correction of older Histories;
- concrete correction API/commands.

## Immediate next discussion target

Start from:

    Supports(H_rep, H_true)

and use `correction-cases.md` as the regression catalog.

The next question is:

> What business observations or requirements make one valid History a sufficient representation of another for correction purposes?

Do not design the full correction API before this relation is understood well enough to reject materially wrong histories.
