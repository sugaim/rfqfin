# RFQ Domain Rework — Handoff

This is the single active working handoff. It is non-canonical.

## Read first

Read in this order:

1. `../domain.md` — canonical Domain authority.
2. `README.md` — working-document rules and discussion method.
3. `sessions/01-rfqcase-positive-flow.md` — completed RfqCase positive-flow design unit.
4. `sessions/02-rfqdraft.md` — completed RfqDraft design unit.

The session records preserve rationale and boundaries. They do not override `../domain.md`.

## Current status

Two design units are complete:

- positive-flow RfqCase;
- pre-publication RfqDraft, including the immediate Domain/Application boundary needed to interpret Draft behavior.

The canonical Domain currently includes:

- the positive-flow RfqCase aggregate/state model;
- immutable RfqTerms/PricingEpisode/Quote/QuotePresentation concepts;
- PresentationOutcome and CaseOutcome separation;
- AssumedTradeDate and TradeDateLag settlement semantics;
- explicit date/chronology invariants;
- the separate RfqDraft Aggregate Root;
- DraftField<T>, RfqDraftData, responsibility fields, lifecycle, and Domain operations;
- CopyDraft, SeedDraftFromCase, and PublishDraft mappings;
- NotionalAmount, CleanPrice, and Rate value semantics.

Do not use this handoff as a substitute for reading `../domain.md`.

## Settled models to treat as fixed

Treat the current positive-flow RfqCase and RfqDraft models as fixed unless the correction discussion exposes a **concrete business contradiction**.

Do not reopen them merely because a different structure might also be possible.

In particular:

- Draft must not be reintroduced into RfqCase.State;
- old Revision/Requested/Confirmed implementation concepts are not current Domain authority;
- WorkingQuote remains outside RfqCase Domain;
- actor authorization does not define Domain operation identity;
- full historical collections are not assumed to be loaded into RfqCase;
- PricingDate and AssumedTradeDate remain distinct;
- correction design must not silently weaken existing positive-flow invariants.

## Relevant settled Application-side Draft policies

These are not RfqDraft Domain invariants, but they were settled during Session 02 and should not be accidentally redesigned while working on correction.

- DraftOwner may edit ordinary Draft content and manage normal Draft lifecycle.
- DraftOwner may grant/revoke ordinary edit access.
- GrantedEditor may edit ordinary RfqDraftData only.
- DraftOwner alone assigns/changes ContactOwner.
- DraftOwner or ContactOwner may manually assign/override QuoteOwner.
- ContactOwner alone may Publish.
- ChangeDraftOwner revokes existing edit grants.
- QuoteOwner routing from SecurityId is Application-side defaulting, not a Domain eligibility invariant.
- A non-default QuoteOwner does not block Publish.
- shared Draft updates should use optimistic concurrency rather than silent last-write-wins.
- Publish must reject a stale observed Draft version.
- publishing the Draft and creating the new RfqCase must be persisted atomically and only once.

These belong in future Application/Persistence design rather than being pulled into correction Domain semantics without a concrete reason.

## Next design target

Design:

    RfqCase correction / reversal / historical amendment

Start from **concrete correction use cases**, not from a generic undo mechanism.

The current Domain deliberately leaves several candidate directions unresolved:

- explicit operation reversal / Revert-style correction;
- restoring or jumping to a prior effective state;
- direct typed amendment/correction of historical facts;
- a combination of reversal plus explicit amendment.

None of these is selected yet.

## Questions the next discussion should answer

The next design unit should determine, from concrete business cases:

- what kinds of mistakes/corrections actually occur;
- whether correction changes current effective state, historical facts, or both;
- which existing Domain operations/facts can be reversed safely;
- how correction interacts with terminal Hit/Away/Cancelled states;
- how references among Terms, Episodes, Quotes, Presentations, and Outcomes remain interpretable after correction;
- whether correction should create new immutable facts, mark earlier facts ineffective, or explicitly amend them;
- how to preserve auditability without turning Domain into unrestricted mutable history;
- which concerns belong to Domain versus Application/audit/persistence;
- which external/irreversible side effects must remain outside in-memory correction semantics.

Do not assume event sourcing, a generic historical-state graph, or unrestricted mutation unless a concrete business requirement justifies it.

## Intentionally deferred beyond correction

Unless correction itself requires them, do not expand scope into:

- exact CancellationReason taxonomy;
- final Presented-Away Case-level disposition taxonomy;
- typed analytics/Feedback taxonomy;
- foreign-market settlement-date context;
- settlement amount/currency/FX semantics;
- first-class Case/Draft lineage;
- exact audit/event schema;
- broad API/DTO/frontend migration;
- the full Application authorization/use-case design.

## Working approach

Use the working method in `README.md`.

For this next unit in particular:

- read canonical docs before relying on session history;
- challenge the settled model only with concrete contradictions;
- separate Domain validity/effective business meaning from Application authorization and Persistence mechanics;
- do not import RfqDraft-specific patterns merely because they worked for Draft;
- discuss one coherent correction problem at a time;
- prefer partial, typed correction semantics over a generic mutation escape hatch;
- before writing canonical changes, re-scan active docs for contradictions and stale statements.

## Why start a fresh session

RfqDraft is now a completed design unit.

Correction is a materially different problem: it concerns the interpretation and modification of historical/effective RfqCase facts rather than assembly of a pre-publication Draft.

Starting correction in a fresh discussion reduces anchoring on Draft-specific design choices while preserving the required background through `../domain.md` and the two session completion records.
