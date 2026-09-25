# Working Design Data

This directory contains non-canonical working material used to carry design discussions across sessions.

## Authority and reading order

The documentation roles are intentionally different:

1. `../domain.md` is the canonical Domain authority.
2. `handoff.md` is the current working handoff: where the design work is now and what should be discussed next.
3. `sessions/*.md` are completion records for finished design units. They preserve useful rationale, boundaries, and unresolved items, but they are not current Domain authority.
4. `../_archive/` contains obsolete or superseded historical material from an earlier documentation/model timeline. It is not the same thing as the working session records in this directory.

When documents disagree on Domain meaning, `../domain.md` wins.

Do not treat an older session record as evidence that a later canonical Domain decision is wrong. Read a session record when the rationale or boundary of a particular completed design unit matters.

## Handoff policy

There is exactly one active `handoff.md`.

It should stay compact and answer:

- what canonical material must be read;
- which design units are already complete;
- what the current design target is;
- which settled models should be treated as fixed;
- which unresolved questions are intentionally in scope next.

Do not turn `handoff.md` into a second copy of `domain.md`.

When a design unit is completed:

1. update canonical documentation first;
2. write a session completion record under `sessions/`;
3. update `handoff.md` for the next design unit.

## Session records

A file under `sessions/` is a completion record, not a transcript or chat log.

A useful session record should normally contain:

- the design goal;
- the major decisions completed in that design unit;
- important refinements or reversals made during discussion;
- Domain/Application/Persistence boundary decisions that matter to future work;
- intentionally unresolved topics;
- which canonical documents were updated;
- why the design unit is considered complete;
- the boundary to the next design unit.

Do not duplicate Git commit history in session records. Git remains the mechanical version history.

Session records are numbered by design-unit order, not by calendar date.

## Working method for design discussions

The following working method proved useful during the RfqCase/RfqDraft redesign and should be preserved unless a later task gives a concrete reason to deviate:

- Read canonical documentation before relying on handoff or historical context.
- Preserve settled models unless a concrete contradiction or new business requirement is found.
- Separate Domain validity and deterministic business meaning from Application authorization/orchestration and Persistence/engineering mechanics.
- Do not create a new Domain abstraction merely because fields behave differently in one operation. Require a meaningful semantic boundary, lifecycle, invariant, or operation distinction.
- Do not split Domain operations merely because actor authorization differs. Split them when their Domain effects or business meanings differ.
- Conversely, do not preserve a broad generic operation when later discussion reveals genuinely different Domain effects.
- Revisit an earlier decision when later requirements invalidate the rationale that supported it.
- Prefer concrete business requirements over speculative generalization.
- Discuss one coherent design question at a time before updating canonical docs.
- Before changing canonical docs, re-scan the active documentation for contradictory normative statements and stale terminology.
- If a proposed change cannot be reconciled with the current canonical structure, stop and discuss the contradiction rather than silently writing around it.

## Current working files

- `handoff.md` — active handoff.
- `sessions/01-rfqcase-positive-flow.md` — completion record for the positive-flow RfqCase design unit.
- `sessions/02-rfqdraft.md` — completion record for the RfqDraft design unit.
