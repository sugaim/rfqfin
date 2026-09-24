# Documentation

This directory separates active design authority from historical material.

## Active authority

- [domain.md](domain.md) — canonical Bond RFQ Domain concepts, invariants, state model, Domain operations, and Domain/Application boundary.

Future concerns should be split by responsibility rather than accumulated into one generic design document. Likely future active documents include:

- `usecases.md` — user/application use cases and workflow composition;
- `application.md` — Application-layer responsibility, authorization, orchestration, and external-context policy;
- `engineering.md` — implementation and engineering policy, if/when a new canonical engineering document is required.

Do not create these files merely to reserve names. Add them when the corresponding design has actually been discussed and settled.

## Current documentation phase

The Domain documentation is currently in a **design-convergence phase**.

During this phase, `domain.md` intentionally keeps some rationale, rejected alternatives, negative decisions, and deferred topics close to the canonical model. This makes the document longer and somewhat repetitive, but that trade-off is deliberate: preserving design intent and preventing obsolete alternatives from being reintroduced is currently more important than minimizing document length.

Accordingly:

- do not split, shorten, or deduplicate `domain.md` merely because it is large;
- do not remove rationale or negative decisions unless their meaning is preserved elsewhere;
- treat repeated explanations as a maintenance cost to monitor, not as an automatic refactoring trigger;
- when a Domain decision changes, update every normative statement that expresses that decision rather than allowing duplicate sections to diverge.

The expected long-term direction is different. Once the Domain is materially stable — in particular after major remaining topics such as `RfqDraft`, correction/exception semantics, and the principal Application use cases have been settled — the documentation should be reviewed for separation of concerns.

At that point, prefer:

- `domain.md` for the current canonical model: scope, aggregate structure, Domain objects/entities, states, invariants, operations, and genuinely unresolved Domain topics;
- a separate rationale/history document (for example `domain-rationale.md`) for rejected alternatives, historical decisions, and extended explanation of why the model has its current shape;
- separate use-case/Application documents when those concerns have enough settled content to justify their own authority.

Split documentation when the Domain is materially stable and duplicated rationale has become a real maintenance risk. Do not split merely to reduce file length or to make the document look cleaner during active design convergence.

## Working handoff

- [handoff.md](handoff.md) — context for the next design discussion. It is intentionally useful to ChatGPT/Codex, but it is not canonical Domain authority.

## Historical material

- `_archive/` — obsolete or superseded documentation retained only for historical context.
- `_archive/refactoring/` — historical implementation/refactoring instructions.

Current source code may still reflect archived concepts. When current code conflicts with `domain.md` on Domain meaning, `domain.md` is authoritative until implementation migration is completed.

Git history is the version history of active documentation.
