# 10. Design Decisions and Rationale

This document records the non-obvious design decisions that materially affect implementation.  
The goal is not to preserve every discussion detail, but to preserve enough rationale that a later implementation does not casually collapse or reinterpret important boundaries.

---

## 1. Separate `RfqStatus`, `QuoteStatus`, and `QuoteRequestReason`

The workflow originally looked like it might need a single large status enum containing states such as:

```text
Active
Quoted
Presented
Amending
Requote
Cancelled
Closed
...
```

That creates composite-status growth because several dimensions are independent.

The chosen split is:

```text
RfqStatus
- Draft
- Active
- Presented
- Cancelled
- Hit
- Away
```

```text
QuoteStatus
- Requested
- Quoted
```

```text
QuoteRequestReason
- Initial
- Revised
- Reopened
- Expired
- Withdrawn
```

Rationale:

- RFQ/customer-facing state and trader-quote workflow are different dimensions.
- `Requote` is not really a stable trader state; it is a reason the quote is Requested again.
- `Hit` and `Away` are useful business-facing terminal statuses and make a separate generic `Closed` display status unnecessary.
- This split avoids combinations such as `AmendingQuoted`, `AmendingPresented`, etc.

---

## 2. Amendment is a Draft Revision, not an RFQ status

Do not introduce `Amending` as an RFQ lifecycle status.

An amendment is represented by:

```text
current confirmed Revision
+
one additional Draft Revision
```

While the Draft exists:

- the current confirmed Revision remains authoritative
- the current quote may remain valid
- trader/customer statuses do not change merely because Sales is editing
- Sales can highlight Draft differences separately

Only when the amendment is Confirmed does the RFQ transition to:

```text
RfqStatus = Active
QuoteStatus = Requested
QuoteRequestReason = Revised
```

Rationale:

- "Sales is editing a future Revision" is not the same kind of state as "customer-facing RFQ is Presented".
- Treating it as a separate status creates unnecessary cross-products with Quoted/Presented.
- Revision itself already carries the correct temporal meaning.

---

## 3. Use coarse lifecycle types only

The domain may use a typed lifecycle such as:

```text
Draft
Open
Cancelled
Closed
```

but should not encode every operational combination into distinct types.

Inside `Open`, fields such as:

- `RfqStatus`
- `QuoteStatus`
- `QuoteRequestReason`
- ownership
- Contact Owner
- Assigned Trader

remain ordinary values.

Rationale:

- Typed lifecycle prevents category errors such as Presenting a Closed RFQ.
- Encoding every orthogonal combination as a type would create combinatorial explosion and make persistence awkward.
- The lifecycle boundary is the useful level of compile-time safety.

---

## 4. `ConfirmedQuote` is immutable

A ConfirmedQuote is the trader-confirmed pricing snapshot at a particular point in time.

It is not rewritten when later lifecycle events occur.

Events such as:

- Presented
- Unpresented
- Withdrawn
- Expired

are represented separately.

Rationale:

- Confirmed values must remain historically reproducible.
- Mutable lifecycle flags on the quote would blur "what was confirmed" with "what happened later".
- Immutable snapshots make search/history and audit easier to reason about.

---

## 5. Current operational state lives in `CaseCurrent`

The system is **not** full event sourcing.

Current state is stored directly in a mutable projection:

```text
CaseCurrent
```

Events are also appended for history and notifications.

Rationale:

- Normal UI/search needs fast access to the current state.
- Reconstructing every Case from all historical events would add complexity without current business value.
- Event history is useful for audit, notification, and investigation, but is not the sole authoritative representation of current operational state.

---

## 6. Persistence event hierarchy does not imply domain inheritance

Persistence uses:

```text
Event
├─ RfqEvent
└─ QuoteEvent
```

The shared parent exists primarily so that:

- all events share a global `EventId`
- notification/reconnect cursor is one-dimensional
- common fields such as occurrence time/actor are stored once conceptually

However, the domain does **not** need a shared Event base class/DU.

Rationale:

- `CaseId` belongs logically to RFQ events.
- `QuoteId` belongs logically to Quote events.
- The common persistence envelope is useful infrastructure, but not necessarily a meaningful domain abstraction.

---

## 7. Event payloads use typed domain values + JSONB persistence

Avoid either extreme:

- one huge nullable event table
- one physical table per event type

Use:

```text
RfqEvent / QuoteEvent
- Type
- Payload jsonb
```

The domain represents event payloads with typed cases/records.

Repository/infrastructure serializes those typed payloads to JSONB.

Rationale:

- Event types will evolve.
- Large nullable schemas become difficult to maintain.
- Table-per-event-type is too heavy for the current scale.
- JSONB keeps persistence flexible while domain types remain explicit.

---

## 8. Expiry is a real business transition, not only a visual warning

Expiry was considered as a UI-only `Review` badge.

The chosen design is stronger:

```text
Quoted -> Requested
QuoteRequestReason = Expired
Presented -> Active, if needed
```

executed by a background worker.

Rationale:

- If expiry is only visual, every write operation must independently remember to reinterpret "Quoted but expired".
- A real state transition makes permissions and subsequent workflow consistent.
- RFQ is sufficiently time-sensitive that automatic expiry is valuable.

Initial implementation still keeps expiry mechanics small:

- same App Server process
- `BackgroundService`
- about 10-second interval
- idempotent transition
- single App Server assumption

---

## 9. Expiry is its own event, even if mechanics overlap with Withdraw

Internally, Expire and Withdraw may reuse common "deactivate current quote / request quote again" logic.

Business history should still record:

```text
Expired
```

rather than synthesizing:

```text
Unpresented
Withdrawn
```

Rationale:

- human actions and time-driven expiry have different business meanings
- audit/history should not imply a user manually performed actions that were automatic

---

## 10. WorkingQuote belongs to Revision

A WorkingQuote is not merely a Case-level scratch area.

It belongs to a Revision.

Rationale:

- quote inputs/results are meaningful against a specific RFQ condition set
- historical revisions may need their old working values when customers return to previous terms
- revision changes should create/seed a new WorkingQuote rather than mutate historical quote work

Confirmed Revision invariant:

```text
Confirmed Revision -> WorkingQuote exists
```

implemented through `EnsureWorkingQuote`.

---

## 11. `EnsureWorkingQuote` is both invariant enforcement and recovery

At Revision Confirm:

- use existing WorkingQuote if present
- otherwise clone configured seed WorkingQuote
- otherwise create empty/default WorkingQuote

Trader access may defensively invoke the same behavior if data is missing.

Rationale:

- an invariant that can only fail permanently is operationally fragile
- the system can cheaply self-heal this particular missing object
- concurrency should still be protected by uniqueness/version rules

---

## 12. Reopen does not resurrect quotes, but restores working values

Reopening a Cancelled RFQ results in:

```text
RfqStatus = Active
QuoteStatus = Requested
QuoteRequestReason = Reopened
```

The old confirmed quote is not reactivated.

WorkingQuote values remain available.

Rationale:

- a previously communicated quote may no longer be valid after cancellation time passed
- forcing re-confirmation makes the trader consciously accept the quote again
- retaining working values avoids unnecessary re-entry

---

## 13. Revision update invalidates the old active quote only on Confirm

While Sales edits a Draft amendment, the previous confirmed RFQ and quote remain operational.

On Revision Confirm:

```text
QuoteStatus -> Requested
Reason -> Revised
Presented -> Active, if needed
```

Rationale:

- Draft edits are not yet agreed/official RFQ conditions
- invalidating a quote as soon as Sales starts typing would make the workflow unstable
- confirmation is the correct atomic boundary

---

## 14. Presentation is customer-workflow state, not trader-pricing state

`Presented` belongs conceptually with the RFQ/customer-facing side.

It means the Contact Owner has protected the current quote from trader withdrawal while it is customer-facing.

It does **not** prove the customer literally saw it.

Rationale:

- the operation is controlled by Contact Owner
- the key behavior is withdrawal protection, not pricing calculation
- presentation does not change the trader's quote from Quoted to another trader state

---

## 15. Do not auto-refresh active grids

Server-side changes should not silently replace data in an integrated grid/editor.

Instead:

```text
server change
-> notify "updates available"
-> user chooses Refresh
-> authoritative state is reloaded
```

FE-only uncommitted edits are discarded on Refresh.

Rationale:

- the same screen is both a live list and editing surface
- invisible replacement of cells while a user is working is hard to reason about
- explicit refresh gives users control over context changes

---

## 16. Keep two change windows

The Changes UI keeps:

```text
Last Refresh
Pending Updates
```

rather than clearing change information on Refresh.

Rationale:

- if Refresh immediately erased the only change list, users could not see what they just incorporated
- two generations are sufficient operationally
- long-term audit remains in Event tables, so FE does not need a permanent change history

---

## 17. SSE is a wake-up channel, not the data source

SSE sends a lightweight indication that something changed.

FE then calls the persisted event query:

```text
GetEventsAfter(lastSeenEventId)
```

Rationale:

- reconnect/catch-up is simpler
- persisted Events remain the reliable source of change information
- transport can later fall back to polling without changing domain/event design
- SSE does not need to carry complete authoritative UI payloads

---

## 18. Separate normal update indication from important toast events

Most changes should only cause:

```text
Updates Available
```

Only important state transitions produce immediate in-app toasts.

Rationale:

- desk-wide RFQ systems can generate many changes
- toast storms are worse than slightly delayed list refresh
- state-changing events such as Close/Withdraw/Expiry/TakeOver deserve immediate attention

---

## 19. Lightweight Command / Query separation

Commands use domain rules and repositories.

Queries may directly read/join DB state into view DTOs.

Rationale:

- history/search screens often need wide joins but no domain mutation logic
- reconstructing a full aggregate for search is wasteful
- update paths still need domain invariants and atomic transactions
- this is a practical split, not an attempt to build a full CQRS platform

---

## 20. Repository abstractions should not expose DB-column operations

Application code should not primarily manipulate persistence concepts such as:

```text
SetCurrentQuoteId(null)
InsertEventRow(...)
SetStatusColumn(...)
```

Use application/domain operations such as:

```text
ConfirmRevision
ConfirmQuote
WithdrawQuote
ExpireQuote
CloseCase
```

and persist resulting state through repositories/unit of work.

Rationale:

- keeps business rules out of infrastructure
- allows persistence structure to change without rewriting the application workflow
- avoids leaking EF/DbContext concepts across layers

---

## 21. Use real PostgreSQL for infrastructure tests

Application/domain tests may mock repositories.

Repository/transaction/query tests should use real PostgreSQL, preferably via Testcontainers.

Rationale:

- PostgreSQL-specific features matter here:
  - partial indexes
  - JSONB
  - transaction behavior
  - concurrency
- EF InMemory/SQLite substitutes can hide the failures that matter most

---

## 22. Keep the first Past RFQ search intentionally simple

Initial design:

- server query
- indexes
- approximately 20k row cap
- FE client-side grid sort/filter
- user narrows search if too broad

Do not implement complex paging/projection/materialized-view infrastructure before it is needed.

Rationale:

- expected total size (~hundreds of thousands of Cases) is small for PostgreSQL
- business query patterns are not fully known yet
- premature search infrastructure would lock in assumptions

---

## 23. Master data and pricing realism are intentionally mocked

The RFQ app should not reimplement:

- holidays
- market conventions
- curves
- pricing conventions

Those belong behind the calculation boundary.

Initial seed/master exists only to make the application usable and searchable.

Rationale:

- this project is validating RFQ workflow, not rebuilding the firm's financial calculation stack
- keeping fake pricing complexity out of the RFQ app makes replacement by the real calculation service straightforward

---

## 24. Authorization logic is centralized because roles will evolve

Business authorization must pass through one policy/service boundary.

Rationale:

- checks depend on several dimensions:
  - Role
  - Desk
  - Contact Owner
  - Assigned Trader
  - Owned
  - future Manager override
- scattering these predicates across endpoints/handlers will become unmaintainable
- future Manager behavior should be added centrally rather than patched into every use case

---

## 25. Unknown business workflows are deliberately deferred

Examples:

- New Bulk
- List / Thread / Portfolio grouping
- Bloomberg inbound format
- Manager overrides
- richer EOD operations
- richer Pricer integration

Rationale:

- desk workflow is not yet known well enough
- the core Case/Revision/Quote design should leave room for these without inventing speculative abstractions now

The implementation should prefer replaceable boundaries over guessed feature behavior.

---

## 26. Persisted change feed must cover cross-session observable transitions

The event feed is the recovery/source-of-truth mechanism for change notification. Therefore any business transition that another active session must be able to discover through Pending Updates must append a persisted event in the same transaction as the state mutation.

At minimum this includes quote confirmation (`Requested -> Quoted`) in addition to Revision Confirm, lifecycle transitions, ownership/responsibility transitions where notification is required, and Presentation/Withdraw/Expire.

Rationale:

- SSE carries only a wake-up signal.
- A state transition without a persisted event can be silently invisible after reconnect.
- Event coverage should be decided by cross-session observability, not by whether the transition feels like an audit event.

---

## 27. Event cursor ordering must follow commit visibility

`GetEventsAfter(lastSeenEventId)` must not rely on a plain identity/sequence allocation order when concurrent event-producing transactions can commit out of order.

The infrastructure must use a commit-order-safe cursor mechanism and prove it with a concurrency integration test where two event-producing transactions obtain/prepare events in one order but commit in the opposite order.

Rationale:

- Advancing a cursor past an uncommitted lower EventId can permanently hide that event after it commits.
- The persisted event feed is explicitly required to provide reconnect recovery without silent data loss.

