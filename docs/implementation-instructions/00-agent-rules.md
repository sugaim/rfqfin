# Agent Rules for Implementation Work

## 1. Scope discipline

Implement only the requested step.

Do not proactively build speculative frameworks/future features.

A step may defer later behavior but must not knowingly create persisted state that violates currently applicable canonical invariants.

---

## 2. Canonical terminology

Use the canonical terms where practical:

- RFQ Case
- Revision
- WorkingQuote
- ConfirmedQuote
- Contact Owner
- Assigned Trader
- Ownership / Owned / Unowned
- Active / Presented / Cancelled / Hit / Away
- QuoteRequested / QuoteConfirmed in Domain
- Requested / Quoted as boundary/display status
- QuoteRequestReason

Do not reintroduce a Domain `bool Owned` as source of truth.

Do not invent composite statuses such as `AmendingQuoted` or generic `Closed` replacing Hit/Away.

---

## 3. Domain rules

Domain business state is immutable from callers.

State changes go through explicit Domain transitions/factories.

Transitions are grouped by business meaning, not by how many objects they update.

Domain must not perform repository queries, current-user lookup, external service calls, or clock lookup.

---

## 4. Application rules

`Rfq.Application` is the Use Case layer.

Application owns:

- loading/querying
- centralized authorization
- ID/time/business-date resolution
- external calculation
- persistence/event orchestration
- transaction boundary

Use typed Domain IDs/value objects inside Application business logic; map raw transport primitives at boundaries.

---

## 5. No accidental architecture expansion

Do not introduce without explicit need:

- MediatR
- event sourcing framework
- message bus
- Redis
- distributed locks
- microservices/separate worker
- generic repository/base repository
- generic Result framework
- AutoMapper solely to remove assignments
- custom logging abstraction
- Shared/Common dumping-ground projects
- separate UseCases assembly
- generic state-machine framework

---

## 6. Transactions

One business use case that changes multiple persisted objects commits atomically.

Repository methods do not independently call `SaveChanges`.

Do not expose `DbContext` to Domain/Application.

Do not hold DB locks during external/heavy calculation.

---

## 7. Date/time

Use:

```text
business dates -> DateOnly
instants        -> UTC DateTimeOffset/timestamp
```

Business today is resolved through configured desk/business timezone (initially JST / Asia/Tokyo), not `DateTime.UtcNow.Date`.

---

## 8. Numeric rules

Use decimal for persisted/application business values such as notional/price/yields/spreads as appropriate.

Mock/internal calculation may use double internally.

---

## 9. API / frontend

Use ASP.NET Core Controllers and code-first OpenAPI.

API request/response DTOs may use primitive transport values.

Do not expose internal Domain state hierarchy directly as transport merely because it exists.

Frontend changes are out of scope unless the requested step explicitly includes them.

---

## 10. Testing

Use:

- xUnit for .NET tests
- real PostgreSQL via Testcontainers for persistence/concurrency tests
- no EF InMemory substitute for PostgreSQL-specific behavior

Every semantic transition change must have focused invariant tests.

---

## 11. Completion report

At the end of a step report:

1. what changed
2. material design choices within allowed freedom
3. migrations
4. commands/tests run and results
5. exact deferred items

Do not proceed to the next step automatically.
