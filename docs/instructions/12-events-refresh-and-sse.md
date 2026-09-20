# Step 12 — Persisted Events, Refresh Windows, and SSE Wake-up

## Goal

Add reliable change notification without auto-refreshing editable grids.

## Read first

- event persistence in `05-persistence-and-events.md`
- notification/runtime behavior in `07-runtime-and-notifications.md`
- Changes UX in `04-ui-ux.md`
- Design Decisions 5, 6, 7, 15, 16, 17, 18

## Implement

### Persistence

Add shared parent:

```text
Event
- EventId
- OccurredAt
- ActorUserId?
```

Child tables:

```text
RfqEvent
- EventId PK/FK
- CaseId
- Type
- Payload jsonb

QuoteEvent
- EventId PK/FK
- QuoteId
- Type
- Payload jsonb
```

Domain does not need common inheritance.

Add typed payloads in Domain/Application and serialize them in Infrastructure.

Initial event coverage should include canonical important events, including at least:

RFQ:

- RevisionConfirmed
- Cancelled
- Reopened
- ClosedHit
- ClosedAway
- OutcomeCorrected
- ContactOwnerChanged
- TakenOver
- PickedUp / Released / AssignedTraderChanged where those changes should appear to another active Trader session

Quote:

- Confirmed
- Presented
- Unpresented
- Withdrawn
- Expired

Backfill command implementations so state mutation + event append are in the same transaction.

Rule: every state mutation that another active session must be able to discover through Pending Updates/reconnect must have a persisted event. Do not rely on SSE-only wake-ups or in-memory notifications for such transitions.

### Event query

Implement:

```text
GetEventsAfter(EventId)
```

with appropriate user/screen filtering for notification relevance.

### Cursor correctness under concurrent commits

The EventId/cursor mechanism must be **commit-order safe**. Do not use a plain PostgreSQL identity/sequence as the sole cursor guarantee: transaction A can allocate a lower ID, transaction B can allocate/commit a higher ID, the client can advance past B, and A can then commit invisibly below the cursor.

Use a DB-serialized cursor allocator (for example a single-row counter locked until commit) or another mechanism that provides the same no-gap property. Keep this mechanism in Infrastructure; Domain/Application only see EventIds/events.

### SSE

SSE sends only a lightweight `changed` wake-up.

On wake-up, frontend fetches events after last EventId.

SSE is not the authoritative state payload.

### Frontend

Add:

```text
Updates Available
```

indicator.

Do **not** reload main grid automatically.

Implement Changes tab with:

- Last Refresh
- Pending Updates

Use cursor boundaries conceptually:

- PreviousRefreshEventId
- LastRefreshEventId
- LatestSeenEventId

On Refresh:

- reload authoritative grid data
- Pending becomes Last Refresh
- clear Pending

Important event toasts only for selected canonical events.

## Completion criteria

Open two browser sessions/users.

Cause a state change in one.

Other session:

- receives Updates Available
- main grid does not silently change
- Pending Updates shows event
- pressing Refresh updates grid
- event moves to Last Refresh

Disconnect/reconnect SSE and verify persisted event fetch catches up.

## Tests

- state + event transactionality
- global EventId ordering/uniqueness
- **reverse commit-order concurrency test** proving a client cannot miss an event when two event-producing transactions complete in the opposite order from their initial work
- child event FK structure
- GetEventsAfter cursor behavior
- SSE endpoint smoke test
- frontend refresh-window reducer/state logic

## Commit boundary

```text
add persisted events and explicit refresh notifications
```
