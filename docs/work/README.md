# Working Design Data

This directory contains non-canonical working material used to carry design discussions across sessions.

## Authority and reading order

The documentation roles are intentionally different:

1. `../domain.md` is the canonical Domain authority.
2. `handoff.md` is the current working handoff: where the design work is now and what should be discussed next.
3. `topics/**/*.md` contain active, cross-session working models or case catalogs for a particular design topic.
4. `sessions/*.md` are completion records for finished design units. They preserve useful rationale, boundaries, and unresolved items, but they are not current Domain authority.
5. `../_archive/` contains obsolete or superseded historical material from an earlier documentation/model timeline. It is not the same thing as the working topic or session records in this directory.

When documents disagree on Domain meaning, `../domain.md` wins.

Do not treat a topic note or older session record as evidence that a later canonical Domain decision is wrong.

## Handoff policy

There is exactly one active `handoff.md`.

It should stay compact and answer:

- what canonical material must be read;
- which working topic material is needed to resume the active discussion;
- which design units are already complete;
- what the current design target is;
- which settled models should be treated as fixed;
- which unresolved questions are intentionally in scope next.

Do not turn `handoff.md` into a second copy of `domain.md` or a duplicate of active topic notes.

## Topic notes

A file under `topics/` is active non-canonical working knowledge for a subject that may span multiple discussion sessions.

Use topic notes when a design problem needs reusable context that should survive a session boundary but is not yet ready for canonical `domain.md`.

Useful topic-note forms include:

- a current semantic/model summary;
- a normalized catalog of concrete cases used to test proposals;
- a focused unresolved subproblem whose reasoning will continue across sessions.

Topic notes should:

- optimize for rebuilding context quickly;
- distinguish current working conclusions from hypotheses/examples/open questions;
- avoid becoming chat transcripts;
- link to detailed case catalogs instead of duplicating them;
- be updated as the active understanding changes.

Once a topic is fully resolved and incorporated into canonical documentation, its working notes may remain as rationale/context, but they no longer override the canonical model.

## Session records

A file under `sessions/` is a completion record, not a transcript or chat log.

A useful session record should normally contain:

- the design goal;
- the major decisions or reductions completed in that design unit;
- important refinements or reversals made during discussion;
- Domain/Application/Persistence boundary decisions that matter to future work;
- intentionally unresolved topics;
- which canonical documents were updated, or why no canonical update was appropriate;
- why the design unit is considered complete;
- the boundary to the next design unit.

Do not duplicate Git commit history in session records. Git remains the mechanical version history.

Session records are numbered by design-unit order, not by calendar date.

When a completed design unit changes canonical Domain meaning:

1. update canonical documentation first;
2. write the session completion record;
3. update `handoff.md` for the next design unit.

A completed exploratory/foundation unit may intentionally produce no canonical change when its purpose is to narrow or structure a problem before a Domain decision is ready. In that case, record why canonical documentation was not changed.

## Working method for design discussions

The following working method proved useful during the RfqCase/RfqDraft redesign and should be preserved unless a later task gives a concrete reason to deviate:

- Read canonical documentation before relying on handoff, topic notes, or historical context.
- Preserve settled models unless a concrete contradiction or new business requirement is found.
- Separate Domain validity and deterministic business meaning from Application authorization/orchestration and Persistence/engineering mechanics.
- Do not create a new Domain abstraction merely because fields behave differently in one operation. Require a meaningful semantic boundary, lifecycle, invariant, or operation distinction.
- Do not split Domain operations merely because actor authorization differs. Split them when their Domain effects or business meanings differ.
- Conversely, do not preserve a broad generic operation when later discussion reveals genuinely different Domain effects.
- Revisit an earlier decision when later requirements invalidate the rationale that supported it.
- Prefer concrete business requirements over speculative generalization.
- Discuss one coherent design question at a time before updating canonical docs.
- Use concrete case catalogs as regression material when evaluating a broad semantic rule.
- Before changing canonical docs, re-scan the active documentation for contradictory normative statements and stale terminology.
- If a proposed change cannot be reconciled with the current canonical structure, stop and discuss the contradiction rather than silently writing around it.

## Current working files

- `handoff.md` — active handoff for historical correction.
- `topics/correction/case-history-and-trace.md` — retained rationale for the canonical RfqCaseRevision / Restore / Trace foundation.
- `topics/correction/correction-cases.md` — active concrete regression catalog for historical correction.
- `topics/correction/operational-correction.md` — completed/superseded starting note for the operational-restore unit.
- `topics/correction/correction-history-model.md` — retained earlier abstract correction exploration; **not** the current semantic starting point.
- `sessions/01-rfqcase-positive-flow.md` — completion record for the positive-flow RfqCase design unit.
- `sessions/02-rfqdraft.md` — completion record for the RfqDraft design unit.
- `sessions/03-rfqcase-correction-foundation.md` — earlier abstract correction-foundation completion record; historical rationale only for the current unit.
- `sessions/04-continued-after-away-provenance.md` — completion record for the ContinuedAfterAway/Away-outcome refinement.
- `sessions/05-operational-history-restore-trace.md` — completion record for the operational chronology / Restore / durable Trace foundation.
