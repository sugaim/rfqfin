# 05. Persistence and Events

## 1. Persistence principles

- Persistence shape may differ from Domain type shape.
- Domain types do not expose DB implementation details.
- Current operational state is stored directly; this is not event sourcing.
- Events provide audit/history and notification feed, not the sole state-rebuild source.
- Confirmed business snapshots are immutable.
- Flattened persistence status columns are projections of the typed Domain state, not a second Domain source of truth.

---

## 2. Logical tables

### `RfqCase`

Primary key: `CaseId`.

Contains stable Case facts such as:

- ClientId
- SecurityId
- SalesId?
- CategorySnapshot (`CategoryId`)
- CreatedAt
- CreatedBy
- CopiedFromCaseId?
- source metadata if added later

### `CaseCurrent`

Primary key: `CaseId`, 1:1 with `RfqCase`.

A flattened current projection may contain:

- lifecycle kind / RFQ status projection
- quote status projection / request reason projection
- CurrentRevisionId
- CurrentQuoteId?
- ClosedQuoteId?
- ContactOwnerId
- AssignedTraderId
- ownership boolean projection
- Version (`bigint`)

This flattened shape is acceptable even though Domain uses `ActiveRfq`, `PresentedRfq`, `QuoteRequested`, `QuoteConfirmed`, and typed `Ownership`.

Mapper logic must derive these columns from Domain state and reconstruct only valid Domain state on load.

`CurrentQuoteId` is operational. `ClosedQuoteId` records the immutable ConfirmedQuote used to resolve a closed Hit/Away case.

### `RfqRevision`

Primary key: `RevisionId`, FK to Case.

Contains:

- Status
- Revision terms
- CopiedFromRevisionId?
- QuoteSeedRevisionId?
- Version
- created/confirmed metadata

### `WorkingQuote`

Primary key may be `RevisionId` to enforce one-per-Revision.

Contains:

- RevisionId
- active mode
- calculated payload
- manual payload
- version
- audit/update metadata

The persistence row may be updated in place even though the Domain object is immutable; repository mapping applies the returned new Domain value to the tracked EF entity.

### `ConfirmedQuote`

Primary key: `QuoteId`, FK to `RevisionId`.

Immutable snapshot.

No mutable Presented/Withdrawn/Expired flags belong on it.

Current supported expiry persistence may remain:

- expiry minutes / null
- resolved ExpiresAt / null

while Domain uses a typed expiry policy.

### `CaseMemo`

1:1 per Case initially.

Contains SalesMemo, TraderMemo, Version, and any needed audit metadata.

### `Category`

Master data:

```text
CategoryId   // stable key
Name         // mutable display name
```

`Security.CategoryId` and `CategoryRouting.CategoryId` reference this master through FKs.

### Other existing tables

Retain current logical roles for:

- `CategoryRouting`
- `CalculationFailureLog`
- `UserGridConfig`
- event cursor / Event tables

---

## 3. StateVersion mapping

Domain/Application use `StateVersion`.

DB remains signed `bigint`/`long` and EF concurrency token.

Map at the Infrastructure boundary.

Do not change DB columns to unsigned types.

---

## 4. Domain rehydration

Persistence reconstruction must not require public mutable setters or public arbitrary `Restore` escape hatches.

Use non-public constructors / `internal Restore` or equivalent and narrowly allow Infrastructure access, e.g. `InternalsVisibleTo("Rfq.Infrastructure")`.

Application code should use public factories/transitions, not rehydration APIs.

If persisted columns represent an impossible combination, mapping should fail as a Domain invariant/data-integrity problem rather than silently constructing invalid state.

---

## 5. Event persistence

Use a thin shared parent **at DB level**.

### `Event`

```text
EventId
OccurredAt
ActorUserId?
```

`EventId` is global for event retrieval/cursor purposes.

The cursor must be **commit-order safe**. Plain identity/sequence allocation before commit is not sufficient by itself.

A DB-serialized cursor allocator held through transaction commit is acceptable. Whatever mechanism is used must be proven by a real PostgreSQL concurrency test that no late-committing event can be permanently skipped.

### `RfqEvent`

```text
EventId -> Event
CaseId
Type
Payload jsonb
```

### `QuoteEvent`

```text
EventId -> Event
QuoteId
Type
Payload jsonb
```

**Do not store `CaseId` on `QuoteEvent`.**

Case identity is derived through:

```text
QuoteEvent.QuoteId
-> ConfirmedQuote.RevisionId
-> RfqRevision.CaseId
```

This prevents the DB from permitting a QuoteEvent whose CaseId and QuoteId refer to different Cases.

Queries that need CaseId join through the relation.

---

## 6. Event types

### RFQ event candidates

- RevisionConfirmed
- Cancelled
- Reopened
- ClosedHit
- ClosedAway
- OutcomeCorrected
- ContactOwnerChanged
- TakenOver
- PickedUp / Released / AssignedTraderChanged where useful

### Quote event types

- Confirmed
- Presented
- Unpresented
- Withdrawn
- Expired

Quote events refer to immutable ConfirmedQuote by `QuoteId`.

Quote Confirm emits `Confirmed` so other sessions can discover Requested -> Quoted through the persisted feed.

State mutation and event append occur in the same use-case transaction.

---

## 7. Authoritative data vs projection

Authoritative business data includes:

- RfqCase / lifecycle state as persisted through tables
- RfqRevision
- WorkingQuote
- ConfirmedQuote
- CaseMemo
- Event / RfqEvent / QuoteEvent
- configuration/master data
- calculation failure log

`CaseCurrent` is the transactionally maintained current operational projection/flattened persistence slice.

Do not rebuild it from events on every request.

---

## 8. Repository boundaries

Application-facing repositories remain business/domain-oriented.

Avoid exposing column operations such as:

```text
SetCurrentQuoteId(null)
SetStatusColumn(...)
InsertQuoteEventRow(...)
```

Application invokes Domain transitions/factories and asks repositories to persist the resulting typed values.

Query-side repositories/readers may project directly into query DTOs.

---

## 9. Unit of Work / transactions

A scoped EF `DbContext` may back multiple repositories.

Application sees `IUnitOfWork` or equivalent.

General rule:

```text
one business use case
-> all related persistence updates/events
-> one atomic commit
```

Examples include:

- Quote Confirm: CaseCurrent + ConfirmedQuote + QuoteEvent
- Initial Confirm: Case/Revision current state + WorkingQuote + event(s)
- Amendment Confirm: revisions + CaseCurrent + new WorkingQuote + event(s)
- Expire: CaseCurrent + QuoteEvent

Do not hold DB locks while performing external/heavy calculation.

---

## 10. Loading strategy

Command-side retrieval loads only state needed for the transition:

- Case facts/current lifecycle
- Current Revision
- pending Draft if relevant
- WorkingQuote/current ConfirmedQuote/seed WorkingQuote as relevant

Do not routinely load all historical Revisions, all quotes, or all Events.

History/search uses query-side DTOs.

---

## 11. DB constraints

Minimum constraints include:

- one CaseCurrent per Case
- at most one Draft Revision per Case
- at most one WorkingQuote per Revision
- valid CurrentRevision FK
- valid CurrentQuote FK when present
- valid ClosedQuote FK when present
- Security -> Category FK
- CategoryRouting -> Category FK
- optimistic concurrency version columns

PostgreSQL partial unique index remains appropriate for one Draft per Case.

Domain rules and DB constraints both protect critical invariants.

---

## 12. Indexing and search projection

Keep current pragmatic indexing strategy for expiry worker and Past RFQ search.

Do not create broad speculative compound indexes or a full copied search model until usage requires it.

A future thin search projection may store references, but do not duplicate every display field prematurely.

---

## 13. Initial indexing

Initial indexes should cover actual operational queries, including:

- expiry worker: current confirmed/quoted items + `ExpiresAt`
- Past RFQ search by date/client/security/category/contact owner/assigned trader/status
- stable PK/FK joins

Do not pre-create every possible compound index. Observe real search patterns and add targeted indexes.

---

## 14. Past RFQ read model

Do not build a large copied snapshot before evidence requires it.

Initial approach:

- ordinary relational joins
- direct query DTO
- indexes
- optionally a very thin reference projection if proven useful

Do not duplicate every display field prematurely or default to materialized views with refresh-management complexity.
