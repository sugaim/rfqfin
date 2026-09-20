# 05. Persistence and Events

## 1. Persistence principles

- Persistence shape is allowed to differ from domain type shape.
- Domain types should not expose DB implementation details.
- Current operational state is stored directly; this is not full event sourcing.
- Events provide audit/history and notification feed, not the only source for rebuilding current state.
- Confirmed business snapshots remain immutable.

---

## 2. Logical tables

### `RfqCase`

Primary key: `CaseId`

Contains relatively stable Case facts:

- ClientId
- SecurityId
- SalesId?
- CategorySnapshot
- CreatedAt
- CreatedBy
- CopiedFromCaseId?
- source metadata if added later

### `CaseCurrent`

Primary key: `CaseId`

1:1 with `RfqCase`.

Contains mutable current projection:

- Lifecycle
- RfqStatus
- QuoteStatus? (operationally meaningful while Open)
- QuoteRequestReason?
- CurrentRevisionId
- CurrentQuoteId?
- ClosedQuoteId?
- ContactOwnerId
- AssignedTraderId
- Owned
- Version

`CurrentQuoteId` is an internal reference to identify the currently operational ConfirmedQuote; UI/business language remains `Quoted`.

`ClosedQuoteId` records the immutable ConfirmedQuote against which a Closed Case was resolved Hit/Away. It is null before Close and remains available after `CurrentQuoteId` is no longer operational.

### `RfqRevision`

Primary key: `RevisionId`

FK to `CaseId`.

Contains:

- Status
- Notional
- SettlementDate
- StandardSettlementDate
- SalesAndTradingMessage
- CopiedFromRevisionId?
- QuoteSeedRevisionId?
- Version
- created/confirmed metadata

### `WorkingQuote`

Primary key may be `RevisionId` to enforce 0..1 per Revision.

Contains:

- RevisionId
- active mode
- calculated payload
- manual payload
- version
- update metadata

### `ConfirmedQuote`

Primary key: `QuoteId`

FK to `RevisionId`.

Immutable snapshot.

No mutable lifecycle-status column is required for Presented/Withdrawn/Expired.

### `CaseMemo`

1:1 per Case is sufficient initially.

Contains:

- SalesMemo
- TraderMemo
- update metadata as needed

### `CalculationFailureLog`

Append-only failure record.

Contains enough information to reproduce the attempted calculation.

### `UserGridConfig`

See UI document.

---

## 3. Event persistence

Use a thin shared persistence parent **only at the DB level**.

Domain types do not need a shared base type.

### `Event`

```text
EventId
OccurredAt
ActorUserId?
```

`EventId` is global and monotonic for event retrieval/cursor purposes.

The notification cursor must be **commit-order safe**. A plain database identity/sequence allocated before commit is not sufficient by itself: concurrent transactions can allocate EventIds in one order and commit in another, causing `GetEventsAfter(lastSeenEventId)` to miss a late-committing lower ID.

The implementation must therefore use a mechanism whose visible cursor order is consistent with commit visibility, for example a DB-serialized event cursor allocator held until transaction commit, or another mechanism proven by integration test to prevent this gap. The exact mechanism is infrastructure-level; the no-loss property is canonical.

### `RfqEvent`

Primary key / FK: `EventId -> Event`

Contains:

```text
CaseId
Type
Payload jsonb
```

### `QuoteEvent`

Primary key / FK: `EventId -> Event`

Contains:

```text
QuoteId
Type
Payload jsonb
```

This arrangement gives:

- one global event cursor
- RFQ/Quote-specific ownership
- no fake `CaseId` on generic Event
- no requirement for a shared domain inheritance hierarchy

---

## 4. RFQ event types

Initial candidates:

- RevisionConfirmed
- Cancelled
- Reopened
- ClosedHit
- ClosedAway
- OutcomeCorrected
- ContactOwnerChanged
- TakenOver

Also reasonable where audit is useful:

- PickedUp
- Released
- AssignedTraderChanged

Payload is typed in the domain and serialized to JSONB by persistence.

Examples:

```text
RevisionConfirmed -> RevisionId
ClosedHit/Away -> QuoteId
OutcomeCorrected -> From, To
ContactOwnerChanged -> FromUserId, ToUserId
TakenOver -> FromTraderId, ToTraderId
```

---

## 5. Quote event types

Initial:

- Confirmed
- Presented
- Unpresented
- Withdrawn
- Expired

All refer to an immutable ConfirmedQuote via `QuoteId`.

`Confirmed` is emitted when a WorkingQuote is snapshotted and becomes the current operational quote. This ensures the persisted change feed can notify other sessions of `Requested -> Quoted`, not only later presentation/withdrawal/expiry changes.

The event itself does not mutate the ConfirmedQuote.

Current state changes are applied to `CaseCurrent` in the same use case/transaction.

---

## 6. Authoritative data vs projection

### Authoritative

- RfqCase
- RfqRevision
- WorkingQuote
- ConfirmedQuote
- Event / RfqEvent / QuoteEvent
- UserGridConfig
- CalculationFailureLog

### Projection

- CaseCurrent
- any future Past RFQ search projection

CaseCurrent is maintained transactionally as the current operational slice.

Do not rebuild it from events on every request.

---

## 7. Repository boundaries

Suggested application-facing repositories:

```text
IRfqCaseRepository
IQuoteRepository
IUnitOfWork
```

Exact API should remain business-oriented, not column-oriented.

Avoid exposing persistence operations like:

```text
SetCurrentQuoteId(null)
SetRfqStatus(...)
InsertEvent(...)
```

as the primary application API.

Instead, application use cases perform domain transitions and repositories persist the result.

---

## 8. Unit of Work / transactions

ASP.NET Core / EF Core implementation may use one scoped DbContext shared by repositories.

Application layer sees an abstraction such as:

```text
IUnitOfWork.CommitAsync()
```

or an equivalent transactional decorator.

Application/domain should not depend on `DbContext`.

General rule:

```text
one use case
-> repository changes
-> one atomic commit
```

A use case such as ExpireQuote may update:

- CaseCurrent
- create Event
- create QuoteEvent

in one transaction.

ConfirmedQuote remains unchanged.

---

## 9. Loading strategy

Do not load an entire historical object graph simply because the Case is conceptually an aggregate.

Command-side retrieval should load only the current state required for the transition:

- Case facts
- CaseCurrent
- Current Revision
- Draft Revision if relevant
- WorkingQuote/current ConfirmedQuote as needed

Do not routinely load:

- all historical Revisions
- all ConfirmedQuotes
- all Events

History/search belongs to query-side DTOs.

---

## 10. DB constraints

Minimum useful constraints:

- one `CaseCurrent` per Case
- at most one Draft Revision per Case
- at most one WorkingQuote per Revision
- valid FK from CurrentRevisionId
- valid FK from CurrentQuoteId if non-null
- valid FK from ClosedQuoteId if non-null
- optimistic-concurrency version columns

PostgreSQL partial unique index is appropriate for one Draft per Case.

Example concept:

```sql
UNIQUE (case_id) WHERE revision_status = 'Draft'
```

Domain rules and DB constraints should both protect critical invariants.

---

## 11. Indexing

Initial indexes should cover:

- expiry worker: quoted/current + `ExpiresAt`
- Past RFQ search:
  - date
  - client
  - security
  - category
  - contact owner
  - assigned trader
  - RFQ status
- stable primary/foreign key joins

Do not pre-create every possible compound index.

Observe actual search patterns and add targeted indexes later.

---

## 12. Past RFQ read model

Do not build a large copied search snapshot before there is evidence it is required.

Initial approach:

- ordinary relational joins
- direct query DTO
- indexes
- at most a very thin projection/reference if needed

If a projection is introduced, keep it minimal, e.g. references such as:

```text
CaseId
DisplayRevisionId
DisplayQuoteId
```

Do not duplicate every display field prematurely.

PostgreSQL materialized views are available but are not the default choice because freshness/refresh management would add unnecessary complexity at this stage.
