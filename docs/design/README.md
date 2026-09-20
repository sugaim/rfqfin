# JPY Corporate Bond RFQ System — Canonical Design

This directory is the canonical design specification for the initial implementation of the internal JPY corporate bond RFQ system.

The canonical design is intended to be usable by humans, coding agents, and future implementation threads. It records:

- the domain and business rules
- state transitions and invariants
- application boundaries
- UI behavior
- persistence/event design
- runtime/integration assumptions
- test/seed strategy
- explicit non-goals
- the rationale behind non-obvious choices

It deliberately does **not** prescribe a step-by-step implementation sequence. That belongs in a separate implementation instruction/plan.

If an implementation instruction conflicts with this canonical design, **this canonical design wins unless the design is explicitly revised**.

## Document map

1. [01-domain-model.md](01-domain-model.md)  
   Core entities, lifecycle, revisions, quotes, ownership, memos, and invariants.

2. [02-state-transitions.md](02-state-transitions.md)  
   RFQ, quote, revision, expiry, cancel/reopen, close, ownership, and presentation transitions.

3. [03-use-cases-and-authorization.md](03-use-cases-and-authorization.md)  
   Application use cases, authorization rules, command/query split, concurrency, and errors.

4. [04-ui-ux.md](04-ui-ux.md)  
   Sales/Trader/EOD screens, grid behavior, refresh/change tracking, search, pricer, and draft UX.

5. [05-persistence-and-events.md](05-persistence-and-events.md)  
   Logical schema, authoritative vs projection data, event persistence, repository boundaries, transactions, indexing.

6. [06-calculation-and-search.md](06-calculation-and-search.md)  
   Calculation boundary, mock behavior, quote calculation flow, security/client search, and standard settlement.

7. [07-runtime-and-notifications.md](07-runtime-and-notifications.md)  
   ASP.NET runtime layout, SSE wake-up, event retrieval, expiry worker, logging/observability.

8. [08-testing-and-seed.md](08-testing-and-seed.md)  
   Test strategy, PostgreSQL integration tests, mock masters, and demo seed data.

9. [09-scope-and-deferred.md](09-scope-and-deferred.md)  
   Explicit non-goals, deferred features, open design space, and future extensions.

10. [10-design-decisions.md](10-design-decisions.md)  
    Rationale for non-obvious choices that should survive implementation handoff.

A concatenated convenience copy is also provided as [design.md](design.md).

## Design principles

- Keep the RFQ business model independent from persistence details.
- Model large lifecycle changes explicitly; avoid encoding every orthogonal state combination as a separate type.
- Confirmed business facts are immutable snapshots; current operational state is represented separately.
- Use optimistic concurrency and transactional use cases rather than long-lived locks.
- Do not make the browser silently replace actively edited data.
- Keep the first implementation deliberately narrow where business requirements are not yet known.
- Preserve enough provenance and history that later reconciliation and operational investigation remain possible.
- Prefer replaceable boundaries over speculative generalization.
