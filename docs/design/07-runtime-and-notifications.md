# 07. Runtime and Notifications

## 1. Initial runtime layout

Initial deployment assumes one App Server instance.

```text
Browser / React
      |
      v
ASP.NET Core App
  - RFQ APIs
  - Search APIs
  - OpenAPI
  - SSE wake-up endpoint
  - Expiry BackgroundService
  - Mock CalculationClient
      |
      v
PostgreSQL
```

Out of scope initially:

- multiple application instances
- Redis/pub-sub
- leader election
- separate worker service
- Kubernetes-specific orchestration

---

## 2. OpenAPI

Server implementation is authoritative.

Generate OpenAPI from ASP.NET Core endpoint/DTO definitions.

The generated contract may later drive FE client generation.

---

## 3. Server change notification model

Do not stream full authoritative UI state over SSE.

Use SSE as a **wake-up signal**.

Flow:

```text
business transaction
-> Event rows committed
-> SSE sends "changed"
-> FE receives wake-up
-> FE calls GET events after last EventId
-> FE updates Pending Updates / shows important toasts
```

The persisted Event feed is the recovery/source-of-truth mechanism for change retrieval.

SSE is only transport signaling.

---

## 4. SSE behavior

SSE is preferred for RFQ responsiveness, but the domain/application design must not depend on SSE.

Requirements:

- reconnect safely
- no silent data loss
- FE retains last fetched EventId
- after reconnect, fetch persisted Events after that ID

Because the initial design uses one server instance, no cross-instance fan-out mechanism is required.

If SSE becomes operationally problematic behind proxy/LB infrastructure, a short polling fallback can reuse the same `GetEventsAfter` API without changing the event model.

---

## 5. Event retrieval

Because persistence has a shared parent `Event` table, FE needs one cursor:

```text
lastSeenEventId
```

The cursor semantics are based on **committed event visibility**, not merely sequence allocation order. Infrastructure must guarantee that advancing the cursor cannot hide a lower EventId that commits later.

Query concept:

```text
GetEventsAfter(lastSeenEventId)
```

Server resolves child RfqEvent/QuoteEvent information and filters as appropriate for the current screen/user.

This endpoint powers:

- Updates Available
- Pending Updates list
- important toasts
- reconnect catch-up

---

## 6. Expiry BackgroundService

Run an ASP.NET Core `BackgroundService` in the same App Server process.

Initial interval:

```text
10 seconds
```

Worker finds current quoted items with:

```text
ExpiresAt <= now
```

and runs the normal `ExpireQuote` application use case.

Do not implement expiry as an ad-hoc SQL state mutation disconnected from domain/application rules.

---

## 7. Expiry idempotency and concurrency

Initial deployment assumes one App Server, but ExpireQuote should still be safe if attempted more than once.

Use state/version preconditions so only a still-current quoted item can transition.

If a human action already Withdrawn/Closed/Revised the RFQ, expiry should do nothing or return a harmless no-op/conflict outcome.

Future multi-instance deployment may require:

- DB locking strategy
- `FOR UPDATE SKIP LOCKED`
- advisory lock
- distributed coordination/pub-sub

but none is required initially.

---

## 8. Logging, audit, observability

Keep four concerns separate.

### Domain audit

Use:

- RfqEvent
- QuoteEvent

### Business calculation failure

Use:

- CalculationFailureLog

### Technical logging

Use .NET standard:

```text
ILogger<T>
```

Do not invent another generic logging abstraction without a concrete need.

### Tracing / metrics

Use .NET standard primitives:

```text
ActivitySource
Meter
```

This allows later OpenTelemetry exporters without changing application/domain code.

---

## 9. Event retention

Initial implementation does not delete Events by count or age.

Retain events.

A few million rows are not in themselves a reason to introduce retention complexity.

Archive/partition/retention may be added if actual growth or compliance policy demands it.

---

## 10. Important event filtering

Do not toast every Desk event.

The server uses current-user/screen scope to decide which events are important to this user.

Normal events can still set `Updates Available`.

Important-event candidates are documented in the UI spec.

---

## 11. Authentication scope

The environment can identify current user and role(s) somehow.

The exact token/header/session mechanism is not part of this initial design.

The application consumes a `CurrentUser` abstraction.

SSE/auth transport should be adapted to the actual hosting environment rather than driving the domain design.
