# Correction History Model

## Status

This is a retained **historical exploration**, not the current correction model.

The later operational-history / Restore / Trace design changed several assumptions that this document treated as open. Current work must start from:

1. `../../../domain.md` — canonical Domain model;
2. `../../handoff.md` — active historical-correction target;
3. `case-history-and-trace.md` — rationale for the current revision/Trace foundation;
4. `correction-cases.md` — concrete regression material.

Do **not** restart the current design from the provisional `Supports(H_rep, H_true)` abstraction or the whole-History-replacement model below. The material is retained only for rationale and ideas that may become relevant to a concrete unresolved question.

This file does **not** override `domain.md`.

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

`C` should not be confused with the exact set of current Application endpoints or the exact set of operations currently implemented as Domain APIs. It is a business-semantic operation vocabulary; current Domain operations may realize only part of it.

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

    R_recorded

denote the currently effective recorded representation, and let:

    H_true ∈ H

stand for the business History that investigation says should be represented.

The metamodel does not require `R_recorded` itself to be a member of `H`. A recorded representation may contain legacy or erroneous structure; the validity requirement applies to the business truth being modeled and to the corrected candidate.

Current scope assumes that the intended business truth is expressible in the current Domain vocabulary. If it is not, that is first a Domain-model enhancement problem.

A correction candidate is:

    H_corrected ∈ H

The semantic target is **not** defined as an inverse operation over `R_recorded`.

Instead, correction aims to produce a valid History that sufficiently represents `H_true`.

The recorded History still matters for:

- identifying what changed;
- authorization/workflow;
- audit/provenance;
- downstream delta handling;
- side-effect reconciliation.

But it is not the sole semantic criterion for correctness.

## Adequacy relation — provisional name and shape

The previous discussion introduced an intentionally abstract relation:

    Supports : H × H -> Bool

with direction:

    Supports(H_rep, H_true)

meaning approximately:

> `H_rep` is a sufficient business representation of the meaning that must be preserved from `H_true`.

The name `Supports` is **provisional**. It may be replaced if the next discussion finds that `Preserves`, `Satisfies`, or a requirement-oriented formulation expresses the model more accurately.

No symmetry, transitivity, equivalence, preorder, or observation-based implementation is assumed.

A corrected candidate must at least be a valid History:

    H_corrected ∈ H

What remains unresolved is the additional adequacy requirement that makes it an acceptable correction.

### Why exact History equality is not the default

Exact History equality may be too strong.

The true business process may contain intermediate internal states that are not needed for the purpose of correction. Conversely, final/current-state equality can be too weak: the case catalog contains examples where equal final lifecycle states hide different customer interactions, outcomes, pricing rounds, ownership changes, or Terms histories.

There may also be cases where current truth is genuinely sufficient and preserving richer history would add cost without business value. The next discussion should therefore avoid assuming one globally maximal preservation rule.

## Immediate modeling problem: what must correction preserve?

Before defining the adequacy relation precisely, organize the current Domain concepts by **why their history may need to survive correction**.

The following requirement dimensions have been identified as candidates.

### Customer-facing business truth

Examples:

- what proposition/value was shown to the customer;
- when it was shown;
- whether it was Hit or Away;
- when the outcome occurred.

Under this view, internal object identity may be irrelevant where the customer-visible proposition is unchanged. For example, two different `QuoteId` values may be equivalent if they represent the same externally relevant quotation.

### Internal business/process meaning

Some facts may matter even when they are not directly customer-visible.

Examples include possible requirements around:

- `ContinueAfterAway`;
- repricing rounds;
- ownership changes;
- Terms changes;
- distinctions encoded in `PricingEpisode.Origin`.

These may be needed for business analysis, workflow interpretation, or future Domain behavior.

Whether each such fact must be preserved is not yet decided.

### Current operational truth

For some correction classes, preserving the correct current business state may be sufficient even if historical detail is intentionally not reconstructed.

This possibility should be evaluated rather than ruled out merely because richer histories are available.

### Referential continuity

Identity/reference integrity is a separate concern from business equivalence.

An internal ID may be irrelevant to customer/business meaning but still be referenced by:

- unaffected persisted facts;
- downstream systems;
- analytics;
- audit/provenance records.

Possible strategies include preserving the ID, retaining an old-to-new correspondence, or updating dependents. Which is appropriate belongs partly outside pure business-history equivalence.

Do not treat referential continuity as proof that an ID must be part of the business adequacy relation.

### Audit / revision history

Prior recorded representations, correction actor/time, and the fact that a correction occurred may need to be retained for audit.

Those requirements belong to revision/audit history unless a concrete business rule makes them part of business History.

They should not be mixed into the corrected History merely because they must be persisted.

## Domain-oriented requirement analysis

The next discussion should walk through the existing Domain concepts and ask, for each one:

> What business purpose, if any, requires this concept or some projection of it to be preserved across correction?

Relevant concepts include:

- RfqTerms;
- PricingEpisode and PricingEpisode.Origin;
- Quote and FirmQuote;
- QuotePresentation;
- PresentationOutcome;
- CaseOutcome;
- ContactOwner;
- root-level dates/context;
- current state.

For each concept, distinguish at least:

- exact identity/value preservation;
- semantic/value preservation with different internal identity allowed;
- structural/occurrence/order preservation;
- current-only preservation;
- no business-history preservation requirement;
- reference-continuity requirement outside business equivalence.

This classification is a **discussion tool**, not yet a formal type system or accepted list of levels.

## Relation should follow requirements, not precede them

The adequacy relation should be derived after the preservation requirements are understood.

One possible eventual shape is still:

    Supports(H_rep, H_true)

but another plausible shape is requirement-oriented, for example:

    Requirements(H_true) -> R
    Satisfies(H_rep, R)

or equivalently a parameterized preservation relation.

No formulation is selected yet.

The important ordering is:

    understand Domain preservation requirements
        ↓
    decide what corrected History must retain
        ↓
    define the adequacy relation
        ↓
    later design correction construction/API

The next discussion should resume at the first step, not by assuming the current `Supports` name or shape is final.

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
- An adequacy relation between corrected and intended History is still needed; `Supports(H_rep, H_true)` is only the current provisional notation.
- The next step is to determine preservation requirements from the existing Domain concepts before fixing that relation.
- Customer-facing truth, internal process meaning, current-state sufficiency, referential continuity, and audit/revision history must not be conflated.
- Command replay/search may be useful as a construction/validation helper.

## Intentionally open

Do not silently decide these while using this note:

- the final name and shape of the adequacy relation currently written as `Supports`;
- which preservation requirements exist for each current Domain concept;
- whether those requirements are expressed through observations, direct predicates, requirement objects, or another structure;
- which business facts must be preserved exactly, semantically, structurally, only currently, or not at all;
- which internal IDs are business-significant versus representational;
- whether transition-only business facts are required;
- exact revision storage/version schema;
- correction authorization/approval workflow;
- downstream propagation/reconciliation;
- multi-Case split/merge/reassociation;
- interaction between Domain evolution and correction of older Histories;
- concrete correction API/commands.

## Immediate next discussion target

Use `correction-cases.md` as regression material, but begin from the current Domain model rather than from the provisional `Supports` predicate.

The next question is:

> For each business concept represented by RfqCase, what must a corrected History preserve, and for what purpose?

In particular, separate:

- customer-facing truth;
- internal business/process meaning;
- current operational truth;
- referential continuity;
- audit/revision history.

Then use those requirements to decide whether the adequacy relation should remain `Supports(H_rep, H_true)` or be reformulated.

Do not design the full correction API before these preservation requirements are understood well enough to reject materially wrong histories.
