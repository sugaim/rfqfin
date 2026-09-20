# Agent Rules for Every Implementation Step

Apply these rules to every step.

## 1. Scope discipline

Implement only the requested step.

Do not proactively build later features, generic frameworks, speculative abstractions, or "future-proof" subsystems.

Do not rewrite unrelated code merely to improve style.

If a later requirement is visible in the canonical design, preserve a reasonable extension point but do not implement the later feature.

A step may defer later behavior, but it must not knowingly violate a canonical invariant that is already applicable to the state it creates. Prefer a minimal real representation over a temporary no-op that leaves canonically invalid persisted state.

---

## 2. Preserve canonical terminology

Use the canonical domain terms exactly where practical:

- RFQ Case
- Revision
- WorkingQuote
- ConfirmedQuote
- Contact Owner
- Assigned Trader
- Owned
- RfqStatus
- QuoteStatus
- QuoteRequestReason
- Presented
- Hit
- Away

Do not invent synonyms such as:

- ActiveQuote as a business term
- RequoteStatus
- Amending status
- generic Closed status replacing Hit/Away

Internal implementation fields such as `CurrentQuoteId` are allowed where the design explicitly permits them.

---

## 3. No accidental architecture expansion

Do not introduce:

- MediatR unless explicitly requested later
- event sourcing framework
- message bus
- Redis
- distributed locks
- separate worker service
- microservices
- repository-per-table
- generic base repository
- generic Result framework for the whole solution
- AutoMapper solely to remove a few assignments
- custom logging abstraction over `ILogger<T>`
- `Shared` / `Common` dumping-ground projects

Simple explicit code is preferred.

---

## 4. Transactions

One business use case that changes multiple persistence objects must commit atomically.

Repository methods do not independently call `SaveChanges`.

Use the scoped EF Core `DbContext` behind Infrastructure repositories and an application-facing `IUnitOfWork`.

Do not expose `DbContext` to Domain or Application.

---

## 5. Date/time rules

Use:

```text
business dates -> DateOnly
instants        -> UTC timestamp
```

Do not use local server time as stored business truth.

Prefer an injectable time abstraction for use cases that depend on "now". Do not add a third-party time library initially unless a concrete need appears.

---

## 6. Numeric rules

Use `decimal` for persisted/application business values such as:

- notional
- price
- displayed yields/spreads where appropriate

The mock/internal calculation implementation may use `double` internally.

Do not attempt to implement production-quality financial numerical conventions in this project.

---

## 7. API style

Use ASP.NET Core Controllers.

API is code-first; OpenAPI is generated from the implementation.

Do not create a separate Contracts project initially.

API request/response DTOs may live in `Rfq.Api`.

---

## 8. Frontend style

Primary technologies:

- React
- TypeScript
- Vite
- Ant Design
- AG Grid Community
- Redux Toolkit / RTK Query

Use Ant Design for surrounding application UI:

- forms
- drawer
- modal
- tabs
- buttons
- date inputs
- select/autocomplete wrappers
- notifications/toasts

Use AG Grid for RFQ tabular workflows.

Do not rebuild a second grid using Ant Design Table for the main RFQ screens.

---

## 9. Main-grid refresh rule

Do not silently replace rows in an actively editable RFQ grid when server notifications arrive.

Later event/SSE work must indicate pending changes and reload only on explicit Refresh.

---

## 10. Testing rule

Every step must add tests appropriate to the code introduced.

Use:

- xUnit for .NET tests
- Testcontainers + real PostgreSQL for PostgreSQL-specific infrastructure tests
- Vitest for frontend unit/component logic where useful

Do not use EF Core InMemory as a substitute for PostgreSQL integration tests.

---

## 11. Completion response from coding agent

At the end of each step, the coding agent should report only:

1. what changed
2. important design decisions made within the allowed scope
3. commands run and results
4. exact manual verification procedure
5. any known limitation that belongs to a later step

Do not proceed into the next step automatically.
