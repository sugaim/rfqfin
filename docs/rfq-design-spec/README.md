# JPY Corporate Bond RFQ System — Canonical Design

This directory is the canonical design specification for the internal JPY corporate bond RFQ system.

It records:

- domain data and invariants
- business state transitions
- application/use-case boundaries
- authorization boundaries
- UI behavior
- persistence/event design
- calculation/search boundaries
- runtime assumptions
- test/seed strategy
- explicit non-goals and rationale

It deliberately does **not** prescribe a historical implementation sequence. Implementation instructions may describe how to reach this design, but if an implementation instruction conflicts with this canonical design, **this canonical design wins unless the design is explicitly revised**.

## Document map

1. [01-domain-model.md](01-domain-model.md) — entities, immutable domain data, lifecycle states, revisions, quotes, ownership, category, memos, versions.
2. [02-state-transitions.md](02-state-transitions.md) — business transition categories and transition rules.
3. [03-use-cases-and-authorization.md](03-use-cases-and-authorization.md) — Application/Domain split, authorization, commands/queries, concurrency, errors.
4. [04-ui-ux.md](04-ui-ux.md) — Sales/Trader/EOD screens, grid behavior, refresh/change tracking, search, pricer, and draft UX.
5. [05-persistence-and-events.md](05-persistence-and-events.md) — logical schema, mapping, event persistence, transactions, constraints.
6. [06-calculation-and-search.md](06-calculation-and-search.md) — calculation boundary, WorkingQuote update flow, security/client search, defaults.
7. [07-runtime-and-notifications.md](07-runtime-and-notifications.md) — ASP.NET runtime, SSE wake-up, event retrieval, expiry worker, observability.
8. [08-testing-and-seed.md](08-testing-and-seed.md) — domain/application/infrastructure tests and seed data.
9. [09-scope-and-deferred.md](09-scope-and-deferred.md) — explicit non-goals and future design space.
10. [10-design-decisions.md](10-design-decisions.md) — rationale for non-obvious choices.

A concatenated convenience copy is also provided as [design.md](design.md).

## Design principles

- Domain data is immutable from callers and represents valid business state.
- Use types to make important invalid RFQ/quote state combinations unrepresentable where practical.
- Classify Domain transitions by **business transition category**, not by the number of objects they touch.
- Application is the Use Case layer: it loads, authorizes, allocates IDs/time, invokes Domain transitions/factories, persists, emits events, and commits atomically.
- Actor/role authorization is centralized in Application; state validity belongs to Domain transitions.
- Confirmed business facts are immutable snapshots.
- Persistence shape may be flattened and does not need to mirror the typed Domain hierarchy.
- Use typed IDs and value objects inside Domain/Application; convert to raw transport/persistence primitives at boundaries.
- Use optimistic concurrency and transactional use cases rather than long-lived locks.
- Do not make the browser silently replace actively edited data.
- Preserve history/provenance sufficient for reconciliation and investigation.
- Prefer replaceable boundaries over speculative generalization.
