# Codex Instruction — 02b: Current-Business-Date Read Snapshot, Realtime Invalidation, and 02a Follow-up

## Position of this task

Repository: `sugaim/rfqfin`

Required baseline:

- 02a implementation: `1b4499c40be6b030a2805219804bd74df38ac155`
- canonical design after the 02b design update: `7bc801a78f7286553cc9c5c70cd3f7bdd2d643df`

Start from `main` at or after `7bc801a78f7286553cc9c5c70cd3f7bdd2d643df`.

Before changing code, read:

- `docs/design.md`

Treat it as the single canonical design document.

Temporary files under `docs/refactoring/` are retained intentionally as implementation/refactoring history. Do not delete them.

This task completes the 02 series unless implementation review later reveals a material follow-up.

---

# Goal

Replace the current per-client, persisted-Event-driven Live refresh path with a bounded current-Business-Date read snapshot and process-local relevant-subscriber invalidation.

Target model:

```text
PostgreSQL
   ↓
dedicated BusinessDate snapshot loader
   ↓
immutable BusinessDateRfqSnapshot
   ├─ Sales projection
   ├─ Trader projection
   └─ routing metadata
       ↓
cached Application query implementations

successful RFQ/business mutation
   ↓
cheap generation invalidation after commit
   ↓
single-flight/coalesced snapshot rebuild
   ↓
atomic swap
   ↓
old/new diff
   ↓
relevant SSE subscribers only
   ↓
frontend authoritative page catch-up
```

Persisted semantic Events remain audit/business-history data.

They are **not** the complete realtime invalidation mechanism and must not be forced to represent Draft autosaves, memo updates, WorkingQuote edits, and every other UI-visible mutation.

---

# Core rules

Preserve these invariants:

1. PostgreSQL remains authoritative business state.
2. `BusinessDateRfqSnapshot` is a derived read model, never a second business source of truth.
3. SSE is wake-up only; it never becomes row-patch authority.
4. A committed mutation does not synchronously rebuild the snapshot inside the business transaction/request path.
5. A known-stale snapshot must not be returned as current authoritative data.
6. Multiple invalidations collapse to the latest generation; do not enqueue one rebuild per mutation.
7. Background refresh and request-triggered refresh share one single-flight coordinator.
8. Database Business Date is authoritative. Do not infer Business Date from system clock.
9. Current-Business-Date Sales/Trader membership uses explicit persisted Business Date facts.
10. Horizontal scaling is not implemented in this task.
11. Do not reintroduce frontend prediction of lifecycle/ownership/quote/version state.
12. A successful write followed by a failed authoritative re-read is **not** reported as a failed write.

---

# 1. Business Date facts and current-worklist membership

## 1.1 Add Revision DraftCreatedBusinessDate

Add a persisted Business Date fact to `RfqRevision`, conceptually:

```text
DraftCreatedBusinessDate : DateOnly
```

Exact property/storage naming may follow existing conventions, but the semantic name should remain explicit.

Rules:

- Initial Draft creation sets the initial Revision `DraftCreatedBusinessDate` to the current authoritative Business Date.
- Explicit `Start Amendment` sets the new pending Revision `DraftCreatedBusinessDate` to the current authoritative Business Date.
- Inline amendment editing that creates the first pending Draft sets it the same way.
- Subsequent Draft edits do not rewrite the value.
- Confirm, Supersede, and Discard do not rewrite or erase it.
- Do not derive it from `CreatedAt`.

Preserve existing `RfqCase.CreatedBusinessDate` semantics:

- it remains null for an unconfirmed initial Draft
- it is established when the initial Draft is first confirmed/opened
- do **not** reinterpret it as Case allocation/Draft creation date

## 1.2 Sales/Trader current-Business-Date membership

The current Business Date snapshot includes a Case when:

```text
Case.CreatedBusinessDate == currentBusinessDate
OR
any Revision.DraftCreatedBusinessDate == currentBusinessDate
```

This is intentionally historical-within-the-day.

Required examples:

- initial Draft created today -> included
- initial Draft created yesterday and still Draft today -> not included merely because it is still open
- initial Draft created yesterday but first confirmed today -> included through `Case.CreatedBusinessDate`
- old Case gets Amendment Draft today -> included
- that Amendment is confirmed later today -> still included
- that Amendment is discarded later today -> still included
- old Case with no Case/Revision Business Date fact for today -> excluded

The membership test must consider historical Revisions, including a Draft that has since become Confirmed/Superseded/Discarded.

Do not silently replace Post Process `Today` semantics with this rule. Post Process keeps the separate semantics documented in `docs/design.md`.

## 1.3 Migration and persistence

Add the required migration/model configuration.

Existing persisted rows need a safe migration strategy.

Do not invent `DraftCreatedBusinessDate` by converting historical UTC timestamps if doing so would pretend to know a business fact that was never persisted.

For development/test data, use an explicit deterministic migration/seed strategy consistent with the project’s current development assumptions.

---

# 2. Application query contracts explicitly receive Business Date

Current active-worklist query ports must make the Business Date requirement explicit.

Conceptually:

```csharp
ISalesRfqQueries.GetAsync(
    UserId salesUserId,
    DateOnly businessDate,
    CancellationToken cancellationToken = default);

ITraderRfqQueries.GetAsync(
    DeskId deskId,
    DateOnly businessDate,
    CancellationToken cancellationToken = default);
```

`GetActiveSalesRfqs` and `GetActiveTraderRfqs`:

1. resolve current Business Date through the existing authoritative `IBusinessDateProvider`
2. pass that `DateOnly` explicitly to the query port

Do not hide “today” inside Infrastructure query implementations.

Do not add an unnecessary generic query context object merely to carry one date.

---

# 3. Dedicated BusinessDate snapshot loader

Create a dedicated Infrastructure loader.

Do **not** build the snapshot by invoking the existing user/desk-specific query implementation once per user or desk.

Conceptual structure:

```text
BusinessDateRfqSnapshotLoader
    ↓ PostgreSQL
BusinessDateRfqSnapshot
    BusinessDate
    Generation
    Sales projections
    Trader projections
    routing metadata
```

The snapshot may contain two representations of the same Case.

That is acceptable:

```text
same persisted Case
  -> SalesRfqListItem-like projection
  -> TraderRfqListItem-like projection
```

They are distinct read models, not two business truths.

The whole snapshot must be immutable after construction and published through an atomic reference/swap.

## 3.1 Routing metadata

The snapshot/diff path must have enough metadata to determine affected audiences without re-querying per subscriber.

At minimum support:

- Sales-originating user
- Contact Owner
- Assigned Trader
- Assigned Trader desk
- old and new values where responsibility/routing changed

Do not force Sales and Trader DTOs into one giant common DTO solely for this.

Internal snapshot wrapper/routing records are fine.

## 3.2 Query optimization while building the snapshot

Use this refactor to improve the current query shape.

Required:

- current-Business-Date membership is applied in DB query logic, not after loading the entire historical RFQ universe
- avoid N+1 loading
- consolidate current projection round-trips where practical
- do not repeatedly execute per-user/per-desk query paths
- use `AsNoTracking` for read-model loading where appropriate

### StateSince

Current Sales/Trader queries materialize all relevant RFQ/Quote Events for target Cases and compute:

```text
GroupBy(CaseId).Max(OccurredAt)
```

in application memory.

Move this aggregation to SQL/database-side projection.

The loader should obtain only the latest relevant timestamp per Case rather than materializing all matching historical events merely to calculate `MAX`.

Do not change the visible `StateSince` semantics.

## 3.3 Cached query implementations

Infrastructure implementations of the Application ports should read from the snapshot and filter in memory:

```text
CachedSalesRfqQueries
  -> snapshot
  -> SalesId / ContactOwner visibility filter

CachedTraderRfqQueries
  -> snapshot
  -> desk filter
```

Names may differ.

Application remains unaware that Infrastructure uses a cache/snapshot.

---

# 4. Snapshot generation and commit invalidation

## 4.1 Mutation path must stay cheap

Do not rebuild the snapshot synchronously inside a mutating use case / Unit of Work.

After a successful RFQ/business commit:

```text
commit succeeds
-> generation++
-> signal refresh coordinator
-> return from mutation path
```

The mutation path should perform O(1)-like process-local invalidation only.

Do not hold a snapshot/cache lock around database rebuild work.

Do not put the snapshot rebuild inside the database transaction.

## 4.2 Signal only after successful commit

Integrate the invalidation with the existing Unit of Work / persistence boundary.

Requirements:

- normal no-semantic-event `SaveChangesAsync` success -> signal after persistence succeeds
- semantic-event transaction -> signal only after the transaction commits successfully
- no signal after concurrency failure
- no signal after rollback
- no signal merely because EF entities became dirty in memory

Do not implement this as a broad `DbContext.SaveChanges` interceptor that also rebuilds the RFQ snapshot for unrelated settings/config writes.

A small Infrastructure abstraction such as:

```text
ICommittedRfqChangeSignal
```

or equivalent is appropriate.

Exact naming is not mandated.

Initially it is acceptable for essentially every successful RFQ/business Unit-of-Work commit to invalidate the snapshot, even if a few writes cause harmless extra rebuilds.

Correctness is more important than premature mutation classification.

---

# 5. Generation semantics: latest-state convergence, not a work queue

Use a monotonically increasing in-process generation.

Example:

```text
snapshotGeneration = 10

10 mutations occur before refresh
currentGeneration = 20

refresh once
-> load latest DB state
-> publish generation 20
```

Do **not** create ten queued refresh jobs.

If mutation occurs while a refresh is running:

```text
refresh target = 20
mutation commits
currentGeneration = 21
refresh finishes
```

generation 20 is not final current state.

The coordinator must converge again to 21.

The exact implementation may loop or schedule another single-flight refresh, but must preserve:

```text
publishedGeneration == currentGeneration
```

before considering the snapshot clean.

A refresh that became stale while loading must not be served as the current generation.

---

# 6. Single-flight behavior

Background refresh and GET-triggered refresh must share the same coordinator/task.

Do not implement separate independent refresh paths.

## 6.1 Clean GET

If:

```text
snapshot.BusinessDate == requested Business Date
AND
snapshot.Generation == currentGeneration
```

serve directly from memory.

## 6.2 Dirty GET

If a newer generation is known:

```text
snapshot.Generation < currentGeneration
```

the GET must:

- join/await the existing refresh if one is in flight
- otherwise start/participate in one refresh immediately
- return only after a current snapshot is available
- never silently return the older generation as current data

A GET should not wait for the normal 500 ms background coalescing window when it already knows it needs fresh state.

This gives read-after-write behavior for the normal:

```text
POST/PUT succeeds
-> frontend authoritative GET
```

sequence without making the mutation wait for snapshot rebuilding.

## 6.3 Burst coalescing

Background invalidation uses an initial coalescing interval of approximately:

```text
500 ms
```

This is typed configuration.

The purpose is to collapse a burst of commits into one DB rebuild.

It is **not** a 500 ms delay added to every synchronous GET.

---

# 7. Snapshot refresh retry and failure semantics

## 7.1 Retry

Snapshot loading may fail transiently due to database contention/locks/timeouts/connection problems.

Use bounded retry.

Required configuration includes at least:

```text
SnapshotRefreshRetryCount
SnapshotRefreshRetryDelay
```

Exact option names may follow project conventions.

Use a conservative bounded default.

Do not build a complex resilience framework.

Normal shutdown cancellation is not retried/reported as an incident.

## 7.2 Failure after retry exhaustion

Keep the last successfully built snapshot object for diagnostics/recovery, but mark the current read model dirty/unavailable.

Do not serve it as current if:

```text
published generation < required generation
```

or if it represents the previous Business Date after rollover.

Current Sales/Trader GETs should fail with an appropriate service-unavailable response rather than silently returning known-stale state.

Use the existing error taxonomy/boundary cleanly; do not leak Infrastructure implementation exceptions directly into HTTP contracts.

## 7.3 Incident reporting

Use the existing development/host incident mechanism:

```text
IIncidentReporter
```

Do not move `IIncidentReporter` into Domain/Application merely so Infrastructure can call it.

Preferred dependency direction:

```text
Infrastructure
  snapshot loader/coordinator
  -> throws/records read-model failure state

API/Hosting BackgroundService
  -> catches background failure
  -> IIncidentReporter.ReportAsync(...)

request-triggered failure
  -> normal API error middleware/boundary
  -> IIncidentReporter
```

Avoid duplicate incident storms for repeated retries of the same outage.

A transition into an unavailable state should be reportable as one coherent incident; later retry failures can be technical logs until recovery/re-entry as appropriate.

## 7.4 Startup failure

If initial snapshot construction fails:

- the web process still starts
- Sales/Trader current-worklist read model is unavailable
- current-worklist GET returns service unavailable
- health/readiness reflects degraded/unhealthy state
- report through `IIncidentReporter`
- background retry continues until recovery

Do not require server restart as the recovery mechanism.

---

# 8. Business Date authority, polling, and rollover

The database Business Date row/value remains authoritative.

Do not calculate rollover from local/system calendar time.

The runtime snapshot coordinator keeps a derived current Business Date only for its read model.

## 8.1 Startup

At startup:

1. read authoritative DB Business Date
2. attempt to build that Business Date snapshot
3. publish atomically on success
4. otherwise enter the unavailable/retry path described above

## 8.2 Polling

Poll authoritative Business Date approximately every:

```text
10 minutes
```

This is configuration.

Business Date changes rarely; do not poll aggressively.

## 8.3 Query mismatch

Application resolves Business Date explicitly before calling the active-worklist query port.

If:

```text
requested Business Date != runtime snapshot Business Date
```

do not blindly use the cache.

Re-check the authoritative DB Business Date.

Correctness requirement:

- if DB says requested Business Date is current, runtime snapshot is behind -> trigger/await rollover
- if DB says runtime/new Business Date is current and caller carried the old date -> treat it as a rollover race and resolve/retry once through the normal Application/query path
- do not load arbitrary historical snapshots through this current-worklist cache

Exact exception/result naming is an implementation choice.

## 8.4 Rollover

On Business Date change:

1. keep the old immutable snapshot object available internally
2. build the new-Business-Date snapshot
3. atomic swap only after successful build
4. current-day reads must not pretend the old-day snapshot is the new day
5. notify connected clients with a Business-Date-changed wake-up
6. frontend re-reads Business Date and relevant page state

If build fails, remain unavailable for the new current day and continue retrying.

---

# 9. Realtime SSE subscriber model

Replace the current model where every SSE connection runs a periodic timer and polls the global persisted Event cursor.

The new SSE path is process-local and event-driven from snapshot publication/diff.

## 9.1 Subscriber registry

Keep a process-local registry of connected subscribers.

Enough subscriber metadata should be available for routing, such as:

- current user ID
- desk
- roles / relevant page context or invalidation interests

Do not broadcast detailed identity-rich mutation payloads to every browser and rely on frontend filtering.

## 9.2 Diff old/new snapshot

After successful snapshot publication, compare old/new read-model/routing state and determine affected audiences.

Routing must account for both **old and new** visibility.

Examples:

- Contact Owner A -> B: A and B may both need wake-up
- Assigned Trader moves from Desk X to Desk Y: both relevant desk audiences may need wake-up
- SalesId/ContactOwner visibility changes: old and new relevant Sales users may need wake-up

Do not derive audience only from the new current row; that loses the old audience after ownership/responsibility removal.

## 9.3 Coarse invalidation categories

Coarse categories are sufficient.

Examples:

```text
sales-list
trader-list
recent-revisions
business-date
```

These are invalidation/wake-up categories only.

Never stream authoritative row patches.

`recent-revisions` should be emitted only when the new committed state can affect that surface, such as confirmed Revision/Quote changes.

## 9.4 Slow subscribers

Do not create unbounded per-client queues.

Use capacity-one/coalesced semantics such as:

```text
changed pending
```

Multiple changes before a client consumes the signal collapse.

## 9.5 Same-user tabs

Do not suppress notifications merely because:

```text
event actor user == subscriber user
```

The same user may have multiple tabs/sessions.

One tab mutating state must be able to wake another tab.

## 9.6 Reconnect

SSE loss must not cause permanent stale data.

On a new/reconnected subscription, force or signal an authoritative catch-up against the current snapshot.

Do not require persisted semantic Event replay for Sales/Trader current-worklist correctness.

---

# 10. Remove the old global Event-driven frontend refresh path

02a intentionally kept the old SSE/Event architecture.

02b should finish the transition.

Current problematic shape includes:

```text
App.tsx EventSource /api/events/stream
-> global remoteChangeVersion
-> GET /events refetch
-> all Live clients page-refetch
```

Rework/remove this as appropriate.

Requirements:

- current Sales/Trader Live invalidation comes from the new relevant-subscriber SSE path
- do not keep both old global polling-driven wake-up and new snapshot-driven wake-up active in parallel
- persisted semantic Events remain available for audit/history uses that still require them
- if `/api/events` GET remains for a separate history/debug use, it must not drive current-worklist Live correctness
- remove dead frontend state such as obsolete `refreshToken`/event-cursor plumbing once no longer used

Do not broadly delete semantic Event persistence.

---

# 11. 02a follow-up fixes to include in this task

The 02a implementation is structurally sound; the following focused issues should be fixed as part of 02b.

Do not create a separate generic frontend state framework.

## 11.1 Do not lose an invalidation that arrives during an in-flight catch-up

Current `useLivePausedRows` single-flights one `refetch`, but if a newer wake-up arrives while that request is already in flight, both callers may share a request that started before the newer commit.

Required behavior:

```text
catch-up starts at observed generation 10
generation advances to 11 while GET is in flight
GET 10 completes
-> run/await another catch-up for 11
```

Coalesce reads, but do not lose the later invalidation.

Use generation/dirty semantics consistent with the server read-model model.

## 11.2 Separate write success from reconciliation failure

Current Sales/Trader runners may catch:

```text
mutation succeeded
authoritative refetch failed
```

and display:

```text
operation failed
```

This is incorrect and can encourage an unsafe retry of an already committed operation.

Refactor so these are distinct:

```text
write failure
vs
write succeeded, latest state could not be refreshed
```

Apply this principle to:

- Sales single operations
- Sales bulk operations
- Trader single operations
- Trader bulk operations
- Initial Draft autosave
- Amendment autosave
- other touched mutation/catch-up paths

The read failure should surface through the page/read-model refresh error path.

Do not fabricate local business state to compensate.

## 11.3 Paused Create New from Existing reconciles the new CaseId

`CreateFromExisting` returns `InitialRfqResponse` containing the newly created CaseId.

Do not discard it and reconcile only the source Case.

In Paused mode, reconcile/add the **new CaseId** returned by the server.

The source Case is not the mutation target merely because it was used as a copy source.

Add a regression test.

## 11.4 Zero-difference Amendment frontend consistency

Server/domain correctly rejects zero-difference Amendment Confirm.

Make frontend surfaces consistent:

- row actions must not expose/enable Confirm Amendment for a zero-difference pending Draft
- context menu/work pane/bulk eligibility remain aligned
- changed-cell highlighting must reflect an actual field difference, not merely the existence of a Draft value
- Discard remains available

In particular, do not highlight an unchanged Sales & Trading Message merely because `draftSalesAndTradingMessage` is non-null.

## 11.5 Autosave conflict recovery remains explicit and usable

02a correctly stops automatic chaining after conflict.

Keep that rule.

However, ensure the operator has a usable explicit recovery path after reviewing authoritative state.

Do not silently rebase preserved local input onto a newer version.

A manual/reselection/reset action may rebuild the case-local coordinator from authoritative state, but the choice must be explicit and testable.

## 11.6 Recent Revisions staleness moves to the new notification source

02a uses persisted Event query data to mark Recent Revisions stale.

After 02b, use the new coarse relevant invalidation category instead.

Preserve:

- closed drawer does not fetch
- relevant change marks stale
- opening loads when unloaded/stale
- open drawer may coalesce refresh
- unrelated RFQ changes do not refresh it

---

# 12. Configuration

Use typed configuration/options instead of scattered constants.

Initial configuration should include the relevant current values.

Conceptually:

```text
RfqReadModel / RfqRuntime options

BusinessDatePollInterval = 10 minutes
SnapshotRefreshCoalesce = 500 ms
SnapshotRefreshRetryCount = bounded configurable value
SnapshotRefreshRetryDelay = bounded configurable value
SearchResultCap = 20_000
RecentRevisionsDefaultLimit = 50
RecentRevisionsMaxLimit = 100
```

Exact class/section names may differ.

Do not introduce a generic string dictionary config bag.

The existing `20_000` search cap should no longer be a private hard-coded constant.

The Recent Revisions default `50` should be centralized rather than duplicated unnecessarily across FE/API.

Do not change the actual product defaults unless required for correctness.

---

# 13. Health / observability

Extend the existing health/readiness behavior enough to expose current read-model availability.

At minimum make it possible to distinguish:

```text
process is alive
vs
current Sales/Trader read model is unavailable/stale
```

Useful internal/debug state includes:

- runtime Business Date
- snapshot Business Date
- current generation
- published generation
- last successful refresh time
- unavailable/last failure state

Do not expose sensitive RFQ data through health endpoints.

Use existing `ILogger<T>`, `IIncidentReporter`, and standard .NET diagnostics conventions.

---

# 14. Single-server assumption and required code comment

This implementation assumes one API process.

Snapshot invalidation and SSE subscriber routing are process-local.

Add a concise implementation comment near the snapshot coordinator / committed-change signal documenting this assumption.

The comment should capture the substance of:

> Current implementation assumes a single API process. Snapshot invalidation and SSE subscriber routing are process-local. If user growth or increased application responsibility requires horizontal scaling, add DB-backed cross-process invalidation/notification (for example PostgreSQL LISTEN/NOTIFY) so each node refreshes its local snapshot and notifies local subscribers. Do not rely on sticky sessions alone for consistency.

Do **not** implement multi-node invalidation now.

Do not add Redis.

Do not add distributed locks or leader election.

---

# 15. Query optimization scope

Required in 02b:

- dedicated snapshot loader
- current-Business-Date DB filtering
- Sales/Trader projection loading once per refresh cycle
- SQL-side `StateSince` aggregation
- reduce obvious avoidable round-trips introduced by the current per-page query implementations
- in-memory user/desk filtering from the snapshot

Recent Revisions SQL optimization:

- the current query loads broad history and applies `Take(limit)` after C# diffing
- 02a already removed it from every ordinary Sales catch-up
- optimize it in 02b only if the semantics can be preserved with a clear, low-risk query
- do not let Recent Revisions SQL rewrite dominate this task
- leaving it as a separate lazy query is acceptable

Do not build a general materialized-history/search platform.

---

# 16. Tests

Do not add a synthetic 100-client load test in this task.

Add focused deterministic tests.

## 16.1 Business Date persistence/membership

Cover:

- initial Draft gets `DraftCreatedBusinessDate`
- initial Draft today appears before Confirm
- prior-day initial Draft does not appear merely because still Draft
- prior-day Draft confirmed today appears through `Case.CreatedBusinessDate`
- old Case Amendment Draft created today appears
- same Draft Confirmed today remains in current-day snapshot
- same Draft Discarded today remains in current-day snapshot
- no current-day Case/Revision fact -> absent
- Business Date is not derived from UTC timestamp

## 16.2 Snapshot loader

Cover:

- one loader build creates Sales and Trader projections
- Sales visibility filtering uses SalesId / ContactOwner as designed
- Trader filtering uses desk routing
- `StateSince` preserves semantics with DB-side aggregation
- no per-user loader calls are required to build the global current-day snapshot

## 16.3 Generation/coalescing

Cover:

```text
generation 10
10 invalidations -> 20
one refresh -> publishes 20
```

and:

```text
refresh target 20
generation advances 21 during load
-> coordinator does not declare clean at 20
-> converges to 21
```

Also cover:

- concurrent GETs while dirty share one refresh task
- clean GET does not hit snapshot loader
- GET while dirty waits for current generation
- background coalescing does not add delay to a synchronous dirty GET

## 16.4 Commit invalidation

Cover:

- successful no-event RFQ Unit-of-Work commit invalidates
- successful semantic-event transaction invalidates after commit
- failed SaveChanges does not invalidate
- concurrency failure/rollback does not invalidate
- unrelated non-RFQ configuration persistence does not accidentally use broad DbContext interception if the implementation separates it

## 16.5 Retry/failure

Cover:

- transient load failure retries up to configured count
- later retry success publishes snapshot
- retry exhaustion keeps read model unavailable/dirty
- GET does not return stale snapshot after retry exhaustion
- startup failure does not terminate process
- background retry can recover
- shutdown cancellation is not incident
- incident reporter is invoked through host boundary for unexpected background failure

## 16.6 Business Date rollover

Cover:

- poll detects changed authoritative DB Business Date
- new-day snapshot is built before swap
- successful swap updates Business Date atomically
- failed new-day build does not masquerade old-day snapshot as current
- mismatch path re-checks authoritative DB state
- business-date-changed wake-up occurs after successful rollover

## 16.7 SSE routing

Cover:

- unrelated subscriber not woken
- relevant Sales user woken
- relevant Trader desk woken
- Contact Owner change wakes old + new relevant audience
- Assigned Trader/desk change wakes old + new relevant audience
- same-user second tab still receives wake-up
- slow subscriber queue is bounded/coalesced
- reconnect causes authoritative catch-up
- Draft/memo/WorkingQuote mutation can trigger realtime invalidation even without semantic Event

## 16.8 02a follow-up regressions

Cover:

- invalidation arriving during in-flight frontend catch-up is not lost
- mutation success + refresh failure is not shown as mutation failure
- Paused `CreateFromExisting` adds/reconciles returned new CaseId
- zero-difference Amendment has no Confirm row action
- unchanged Draft fields are not highlighted as changed
- Amendment Discard remains available
- autosave conflict stops automatic retry and explicit recovery does not silently rebase
- Recent Revisions stale state is driven by new relevant invalidation rather than global persisted Event polling

---

# 17. Non-goals

Do not include these in 02b:

- API directory reorganization into `Endpoints/Hosting/Common`
- Application directory reorganization
- EOD endpoint cleanup
- removal of unused CategoryRouting / Revision-history APIs
- broad generated-client / handwritten `services/api.ts` cleanup
- general API versioning cleanup
- horizontal multi-server implementation
- PostgreSQL LISTEN/NOTIFY implementation
- Redis
- distributed cache
- distributed locks
- leader election
- full event sourcing
- general job queue framework
- arbitrary historical BusinessDate snapshots
- sophisticated Recent Revisions materialization
- 100-client/load/performance benchmark harness
- broad CSS/UI redesign

Those belong to later cleanup/API phases unless required narrowly for compile/correctness.

---

# 18. Completion criteria

02b is complete when all of the following are true:

1. Sales/Trader active reads are served from a current-Business-Date immutable snapshot.
2. Snapshot membership follows persisted `CreatedBusinessDate` / `DraftCreatedBusinessDate` semantics.
3. Query ports receive Business Date explicitly.
4. Mutation commit invalidation is cheap and occurs only after successful persistence/commit.
5. Rebuild is generation-based, single-flight, coalesced, and converges to latest state.
6. Dirty GET waits for fresh state instead of returning known-stale data.
7. Snapshot failures retry and ultimately surface as unavailable rather than stale success.
8. Startup can remain alive while the read model is unavailable and recover later.
9. Business Date rollover uses DB authority and 10-minute proactive polling.
10. SSE wakes only relevant process-local subscribers based on old/new snapshot diff.
11. Realtime correctness no longer requires a persisted semantic Event for every visible mutation.
12. Old global Event-polling Live refresh is not left running in parallel.
13. `StateSince` aggregation is pushed to SQL.
14. 02a follow-up issues listed above are fixed.
15. Tests cover correctness/races/failure semantics without introducing a synthetic load test.
16. The required single-server/cross-process-invalidations comment exists near the implementation boundary.
17. `docs/design.md` remains consistent with the implementation.
