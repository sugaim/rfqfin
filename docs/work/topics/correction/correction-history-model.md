# Correction History Model

## Status

This is a non-canonical working note for the RfqCase correction design unit.

It records the motivation, external research, case analysis, and the semantic model currently being developed. It is intentionally earlier than a concrete correction API or canonical Domain design.

Read together with:

- `../../handoff.md` for the active correction goal and design boundaries;
- `../../../domain.md` for the current canonical positive-flow Domain model;
- `../../README.md` for working-document rules.

The active goal from the handoff is to design **RfqCase correction / reversal / historical amendment** from concrete business cases, while preserving the current positive-flow model unless correction reveals a concrete contradiction.

## Why this topic became a separate modeling problem

The initial question was whether correction could be represented as a set of explicit reverse/amend operations over the current RfqCase model.

A fairly large set of correction cases was examined before choosing a direction. The cases included, among others:

- an erroneously recorded Hit that should not exist;
- a real Hit whose date/time was recorded incorrectly;
- a corrected Hit time that invalidates the recorded Hit under quote validity;
- a recorded Hit that should instead have been Away followed by continued pricing;
- a recorded Away/continue path that should instead have remained on the original pricing episode;
- an incorrect Quote value where the Presentation and eventual Hit were otherwise real;
- incorrect Terms where pricing actually used the correct Terms;
- incorrect Terms where pricing actually used the wrong Terms and business truth therefore must be resolved outside the system;
- a real Terms change or repricing that was omitted from the record;
- an incorrect Presentation date;
- an ownership value that was recorded incorrectly versus an ownership handoff that actually happened but was not recorded;
- cancellation versus normal close errors;
- a missing late/EOD close that may be ordinary positive flow rather than correction;
- ValidUntil mistakes and their effect on expiry or Hit validity;
- an erroneous Presentation that incorrectly moved a Case from Inquiry to Negotiating;
- historical ContactOwner mistakes whose authorization meaning belongs primarily to Application;
- confusion between InvalidateQuote and RequestRepricing;
- confusion between RollPricingDate and ChangeAssumedTradeDate;
- two real episode-changing operations recorded as one;
- one real episode-changing operation recorded as two;
- an incorrect OpenDate or BusinessEntity;
- correction of a previous correction;
- upstream/master-data errors;
- a wrong current FirmQuote or current Terms while the Case remains Open;
- a correction where only part of an earlier correction was wrong;
- one recorded Case that should have been two;
- an invalid/duplicate Case versus a valid Case with many incorrect historical facts;
- facts attached to the wrong one of two real Cases;
- duplicate/split Cases whose histories are distributed, duplicated, or mixed.

These cases exposed two important properties.

First, correction is not reliably characterized as “undo the last operation.” The incorrect portion may be historical rather than current, an earlier correction may itself need correction, and the correct history may preserve some facts while removing or re-associating others.

Second, defining a complete family of precise correction operations appears difficult and potentially brittle. A correction API that mirrors every future Domain concept or every possible historical mistake would tend to grow together with the Domain and could become a barrier to future model changes.

This does not mean explicit correction operations are never useful. It means they should not be assumed to be the semantic foundation of correction.

## What was learned from external approaches

Several existing approaches were reviewed for useful ideas. None maps directly onto this RFQ problem, but each contributes a useful implication.

### System-versioned / temporal persistence

SQL Server system-versioned temporal tables keep a current row and automatically retain previous row versions in a history table. This is useful evidence that **mutable effective truth and immutable historical record can coexist** without making every historical version part of the business object model.

Reference:

- https://learn.microsoft.com/en-us/sql/relational-databases/tables/temporal/overview

Implication here: retaining prior representations for audit does not require correction itself to be modeled as a Domain transition.

### Bitemporal history

Bitemporal modeling distinguishes the history of what should have been true in the business world from the history of what the system believed or recorded at different times. Martin Fowler describes this as actual/record history, also commonly called valid/transaction time.

Reference:

- https://www.martinfowler.com/articles/bitemporal-history.html

This distinction is particularly relevant to RFQ correction.

A corrected RFQ history answers a question like:

> Given what is now known, what business history should be treated as having happened?

The sequence of recorded revisions answers a different question:

> What did the system believe at each point in time?

The current direction therefore does **not** identify business History with revision/audit history. A full bitemporal database design is not implied; the important implication is the separation of these two meanings.

### Explicit trade cancel/correct protocols

FIX defines explicit Trade Cancel and Trade Correct semantics, with references to the execution being cancelled or corrected and chaining rules for repeated corrections.

Reference:

- https://www.fixtrading.org/online-specification/trade-appendix/
- https://www.fixtrading.org/wp-content/uploads/download-manager-files/FIX-Latest-Specification-Trade.pdf

Implication here: explicit typed correction and provenance chains can work well when the corrected business object and correction semantics are tightly standardized. This remains a useful pattern for specific RFQ facts, but does not by itself solve arbitrary historical Case correction.

### Workflow-instance modification

Camunda exposes process-instance modification operations such as starting before/after an activity or cancelling an activity instance.

Reference:

- https://docs.camunda.org/javadoc/camunda-bpm-platform/7.24-SNAPSHOT/org/camunda/bpm/engine/runtime/ProcessInstanceModificationBuilder.html

Implication here: operational repair of a running process can be powerful, but a generic “move/skip/cancel anywhere” mechanism is not a good substitute for business semantics. Correction must still define what resulting history is acceptable.

### Redrive / resume

AWS Step Functions redrive continues an unsuccessful execution from the failed point, preserves successful prior results, and reruns only the unsuccessful suffix under explicit eligibility constraints. If the workflow definition changes, a new execution is required.

Reference:

- https://docs.aws.amazon.com/step-functions/latest/dg/redrive-executions.html

Implication here: preserving a valid prefix and reconstructing only an affected suffix can be a useful implementation strategy. It is not sufficient as the general semantic definition because RFQ correction can alter facts earlier than a simple failed suffix.

### Process-mining conformance and alignment

Process-mining tools such as PM4Py compare an observed trace with a process model and support replay/alignment-based conformance checking.

Reference:

- https://github.com/process-intelligence-solutions/pm4py/blob/release/docs/source/api.rst

Implication here: a recorded or desired trace can be compared with the valid business transition model, and a nearby valid trace can potentially be constructed. This is relevant to correction tooling even if the RFQ Domain itself does not become a process-mining model.

### Classical planning

Classical planners such as Fast Downward search for an action sequence from an initial state to a goal under action preconditions/effects.

Reference:

- https://www.fast-downward.org/latest/documentation/planner-usage/

Implication here: once desired correction facts are known, an Application-side helper could search for a sequence of ordinary Domain commands that constructs a valid corrected history. The command sequence would be a construction witness, not a claim about what historically happened.

### Other approaches considered

Event-sourcing compensation/replay, model finding, and labeled-transition/process-algebra style reasoning were also considered. They reinforce useful ideas such as append-only provenance, explicit state-transition validity, trace equivalence, and constraint-based reconstruction, but no decision has been made to adopt any of those architectures.

## Why revision became attractive

A strong candidate implementation direction emerged from the case analysis: treat a correction as producing a new **whole-Case history representation**, while retaining prior representations immutably.

This has several attractive properties.

### Immutability and correction-of-correction

Instead of mutating an old historical object graph in place, each accepted correction can produce another immutable representation.

Conceptually:

    Revision 1 -> History H1
    Revision 2 -> History H2
    Revision 3 -> History H3

If Revision 2 was partly wrong, Revision 3 does not need to “undo the undo.” It can simply express the better current understanding of the business history.

### It matches the business question

The business question is usually not:

> Which compensating operation should be applied to the current internal graph?

It is closer to:

> Given the evidence now available, what should the Case history be understood to have been?

That favors constructing the intended result over encoding correction as a procedural inverse of the mistake.

### It reduces coupling between correction and future Domain growth

If correction is modeled as a parallel family of field-specific amend/reverse operations, every new Domain fact may require new correction machinery.

A history-level replacement model can instead reuse the normal validity semantics of the Domain and place much of the complexity in constructing and validating a candidate corrected history.

### Finite Case histories are a favorable property

At any point in time, an individual RFQ Case has a finite business history.

That makes a complete-history value or snapshot conceptually tractable: a revision can denote one finite candidate History rather than an unbounded live process.

This is not yet a claim about the optimal physical storage format. Persistence may later share structure, store deltas, or otherwise optimize representation. The useful point is that the **logical correction unit can remain the finite whole History** even if storage is not a naïve full copy.

## Revision is not currently a Domain transition

The discussion initially used “Revision” as though it might be a first-class Domain concept. The current direction is more conservative.

A revision/version number can be understood as a Persistence/Application coordinate over immutable representations:

1. an effective recorded History exists;
2. business investigation establishes what the corrected business history should mean;
3. a new valid History is constructed;
4. the new History is checked against the required correction semantics;
5. the new History becomes the effective representation;
6. previous representations remain available for audit/provenance.

Under this view, **Revision is not itself an RfqCase business transition**.

This is deliberately separate from the question of whether a future business requirement may introduce a first-class correction/amendment fact. Nothing currently requires that stronger commitment.

## Correction is about realizing the intended history, not replaying the “right correction operation”

This is the main conceptual shift.

The semantic target of correction is not:

    old recorded state
      + the correct correction command
      = corrected state

Instead it is closer to:

    expected business truth
      -> construct an acceptable valid History
      -> make that History effective

The old recorded History remains important for:

- identifying what changed;
- authorization and operational workflow;
- explaining/auditing why a revision was created;
- determining downstream deltas;
- deciding which external side effects may need follow-up.

But the old History is not, by itself, the semantic criterion for whether the corrected History is correct.

## Semantic model currently being defined

The current discussion is deliberately defining a small metamodel before defining concrete RFQ correction operations.

### State

Let:

    S = business state

For RfqCase, this should be understood as a complete effective business state sufficient to determine future Domain behavior, not merely the enum-like RfqCase.State variant.

### Business command / operation

Let:

    C = business command / operation

and:

    δ : S × C ⇀ S

where δ is partial because some operations are invalid from some states.

The intended reading is:

> δ executes a business-permitted state transition.

C should not be read as “the exact set of endpoints currently exposed by the Application.” The current Application may support only part of the broader business operation vocabulary.

### History

Define the set of valid Histories as finite state sequences connected by business-permitted operations:

    H =
    {
      (s0, ..., sn)
      |
      for every i < n,
      there exists ci in C such that δ(si, ci) = s(i+1)
    }

History therefore contains **States**, not the command sequence itself.

Commands witness that adjacent states can be connected by a valid business transition. They are useful for construction and validation, but are not automatically part of business History.

This is intentional. If a distinction between two transitions must survive historically, the preference is to represent that distinction in business State where possible. PricingEpisode.Origin is an existing example: the resulting state preserves why a new pricing episode exists.

If a concrete future case exposes business-significant transition meaning that cannot reasonably be recovered from states, the model may need an explicit transition observation/fact. That is an escape hatch, not a current assumption.

### Support relation

Let:

    Supports : H × H -> Bool

with the intended direction:

    Supports(H_rep, H_true)

meaning:

> H_rep is an acceptable representation of the business meaning that must be preserved from H_true.

This relation is intentionally abstract at this stage.

No symmetry, transitivity, equivalence, or observation-based implementation is assumed yet.

The corrected History should satisfy:

    H_corrected in H

and:

    Supports(H_corrected, H_true)

The current scope assumes that the business truth being corrected is expressible in the current Domain vocabulary. If the real-world fact cannot be represented by the Domain at all, that is first a Domain-enhancement problem rather than merely a correction problem.

### Why Support is not simple History equality

Correction may not need to reproduce every internal intermediate state that actually occurred.

For example, the true process might contain pricing loops or internal transitions that are not required to preserve the business meaning relevant to correction.

Therefore:

    H_corrected == H_true

may be stronger than necessary.

Conversely, comparing only final current state is generally too weak: two Cases can end in the same state while having materially different customer interactions, outcomes, or historical facts.

The Support relation is intended to capture the required middle ground.

Exactly what must be preserved is **not yet settled**.

## Early observation discussion — deliberately unresolved

The discussion briefly tested how Support might later be defined through observable business facts.

Examples considered include:

- existence or absence of customer Presentations;
- what was presented and when;
- Hit/Away business outcomes and their timing;
- ordering of business interactions;
- identity/reference relationships where identity is itself business-significant;
- values such as Quote/Terms content.

An important emerging distinction is that internal Domain identity and business-history equivalence need not be the same thing.

For example, in a customer-facing interpretation, it may matter that “99.75 was presented at 14:30 and later Hit,” while the particular internal QuoteId or PresentationId used to represent that interaction may not itself be part of the business equivalence relation.

This is only an illustration. No concrete Support rule for Presentation, Quote, Outcome, Terms, or Episode has been accepted yet.

The next design work should continue here rather than silently turning these examples into rules.

## Application-side command replay / planning may reduce correction workload

Although command sequences are not the semantic definition of History, they may still be operationally useful.

A possible correction workflow is:

1. business users establish the facts that the corrected History must express;
2. Application derives constraints or target observations from those facts;
3. Application searches for or constructs a sequence of ordinary Domain commands;
4. the commands are replayed from a suitable starting state to construct a valid candidate History;
5. the candidate is validated against the required Support relation;
6. only unresolved or ambiguous facts are surfaced for manual business judgment.

This resembles replay/alignment/planning techniques found in process mining and classical planning.

The goal would **not** be to infer historical truth from the Domain automatically. Multiple valid command sequences may exist, and the system cannot decide business truth merely because one sequence is legal.

The potential value is narrower but important: once the intended truth is known, the system may be able to minimize manual correction work by using the ordinary positive-flow Domain to construct as much of the corrected History as possible.

This would also preserve an important safety property: correction tooling can reuse normal Domain invariants rather than implementing unrestricted graph mutation.

## What is currently decided versus open

Current working direction:

- Correction is not assumed to be generic undo.
- Business History is modeled as a finite sequence of valid business States.
- Business operations connect valid adjacent states but are not themselves automatically stored as History.
- Correction aims to make an acceptable corrected History effective, not to discover a unique inverse operation over the old record.
- Whole-History revision/replacement is a strong implementation direction because it supports immutability, correction-of-correction, and finite-history reasoning.
- Revision/version identity is currently treated as Application/Persistence machinery rather than core RfqCase Domain semantics.
- The old recorded History matters operationally and for provenance, but semantic adequacy is judged against intended business truth.
- Command replay/search may become an Application helper for constructing a valid candidate History.

Still open:

- the concrete definition of `Supports`;
- whether Support should be defined through a family of observations, direct predicates, or another formulation;
- which customer-facing and internal facts must be preserved;
- where object identity is business-significant versus merely representational;
- whether any transition-only business facts are required beyond state sequences;
- exact representation and persistence of revisions;
- authorization and review/approval workflow for correction;
- downstream change propagation after an effective History changes;
- cross-Case correction, split/merge, and reassociation;
- how Domain enhancement interacts with correction of older histories.

## Immediate next discussion target

Continue from:

    Supports(H_rep, H_true)

without yet designing a complete correction command API.

The next question is:

> What business observations or requirements make one valid History a sufficient representation of another for correction purposes?

Concrete RFQ examples should be used to answer that question, but examples must remain examples until an explicit preservation rule is accepted.
