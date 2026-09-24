# Documentation

This directory separates active design authority from historical material.

## Active authority

- [domain.md](domain.md) — canonical Bond RFQ Domain concepts, invariants, state model, Domain operations, and Domain/Application boundary.

Future concerns should be split by responsibility rather than accumulated into one generic design document. Likely future active documents include:

- `usecases.md` — user/application use cases and workflow composition;
- `application.md` — Application-layer responsibility, authorization, orchestration, and external-context policy;
- `engineering.md` — implementation and engineering policy, if/when a new canonical engineering document is required.

Do not create these files merely to reserve names. Add them when the corresponding design has actually been discussed and settled.

## Working handoff

- [handoff.md](handoff.md) — context for the next design discussion. It is intentionally useful to ChatGPT/Codex, but it is not canonical Domain authority.

## Historical material

- `_archive/` — obsolete or superseded documentation retained only for historical context.
- `_archive/refactoring/` — historical implementation/refactoring instructions.

Current source code may still reflect archived concepts. When current code conflicts with `domain.md` on Domain meaning, `domain.md` is authoritative until implementation migration is completed.

Git history is the version history of active documentation.
