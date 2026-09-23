# JPY Corporate Bond RFQ System — Engineering

This file is the engineering companion to `docs/design.md`.

- `docs/design.md` is authoritative for business meaning, invariants, workflow, authorization, UX semantics, persistence/business facts, scope, and architecture that should survive implementation changes.
- `docs/engineering.md` is authoritative for implementation policy: source ownership, API/OpenAPI contracts, DTO conventions, multi-item execution shape, generated-client policy, runtime/read-model mechanics, testing/tooling, and deployment assumptions.

Both files are updated in place. **Git history is the version history.** Files under `docs/refactoring/` are historical implementation instructions and are not active authority.

---

# 01. Engineering principles

1. **Implementation follows ownership before framework artifact type.**
2. **ASP.NET is the source of truth for the private HTTP contract; OpenAPI is the generated contract artifact.**
3. **Semantic identity justifies sharing; shape equality alone does not.**
4. **Operation requests are use-case-specific by default.**
5. **Multi-item execution is operation-specific, per-Case, and never a heterogeneous command bus.**
6. **Generated transport code is an adapter, not business authority.**
7. **The Web reconciles authoritative server projections after mutations rather than predicting business state.**
8. **Current-Business-Date snapshot/runtime mechanics remain Infrastructure/Hosting concerns.**
9. **Generated artifacts are committed and reproducible.**
10. **Testing follows the real boundary: Domain pure tests, Application orchestration tests, PostgreSQL Infrastructure tests, API contract tests, and focused FE tests.**

---

# 02. Source organization

## 1. Application

Target ownership:

```text
Rfq.Application/
  UseCases/
    Rfqs/
      Creation/
      Amendments/
      Lifecycle/
      Ownership/
      Responsibility/
      Memos/
    Quotes/
      Confirmation/
      Lifecycle/
      Working/
      Expiry/
      History/
    Worklists/
      Sales/
      Trader/
    Search/
    PostProcess/
    ReferenceData/
    Settings/
      Grid/
    Pricing/
  Abstractions/
  Authorization/
  Errors/
  Common/
  DependencyInjection.cs
```

Rules:

- `UseCases` contains Application-provided capabilities.
- capability-specific ports live beside the use case that owns them.
- `Abstractions` is for genuinely cross-capability technical ports.
- `Common` is for genuinely cross-use-case execution/result machinery, not miscellaneous types.
- there is no top-level `Bulk/` or `Queries/` ownership bucket.
- Present/Unpresent follow Domain transition ownership under RFQ Lifecycle.
- Withdraw/Expire belong to Quote lifecycle.
- single and multi-item forms of one operation are colocated.

## 2. API

Target ownership:

```text
Rfq.Api/
  Program.cs
  Endpoints/
    Rfqs/
    Worklists/
    Search/
    PostProcess/
    ReferenceData/
    BusinessDate/
    Me/
    Pricing/
    System/
  Hosting/
    Incidents/
    ...
  Common/
```

Rules:

- `Endpoints` contains externally exposed HTTP capabilities.
- `Hosting` contains process/runtime ASP.NET infrastructure and workers.
- `Common` contains truly shared API-level contracts.
- no giant catch-all RFQ controller.
- Business Date is its own capability, not ReferenceData or System.
- Me identity, quote settings, theme settings, and grid config may share the `/api/me` URL family while remaining physically separate controllers.

## 3. Web

Page ownership remains:

```text
src/
  app/
  pages/
    sales/
    trader/
    post-process/
  shared/
    ui/
    grid/
  services/
  generated/
  test/
```

Page-specific code stays under its page and is grouped by human-recognizable concepts. Avoid page-local generic `components/`, `hooks/`, `utils/`, and `types/` buckets merely for classification.

`shared` is earned by genuine cross-page reuse. A top-level `features` area is reserved for a genuinely independent cross-page user feature.

---

# 03. HTTP and OpenAPI contract

## 1. Authority and compatibility

The API is private to this application unless deliberately changed later.

The application has not been released. Current internal contracts may be changed directly when a cleaner target contract is chosen. Do not add compatibility aliases, dual routes, legacy DTO fallbacks, or speculative defensive adapters solely for hypothetical older clients. Defensive checks remain appropriate where they protect current invariants or genuinely untrusted/external input.

Therefore:

- ASP.NET endpoint/DTO definitions are the HTTP source of truth.
- OpenAPI is generated from the server and committed.
- do not preserve dead/private route aliases solely for backward compatibility.
- route shape may be cleaned when Application/use-case identity becomes clearer.

Broad route families are:

```text
/api/rfqs/...
/api/worklists/...
/api/search/...
/api/post-process/...
/api/reference-data/...
/api/business-date
/api/me/...
/api/pricing/...
/api/system/...
```

## 2. operationId

Every endpoint has an explicit stable `operationId`.

For endpoints corresponding to a public Application use case:

```text
operationId = public Application use-case name
```

Examples:

```text
PresentRfqs
ConfirmQuotes
StartAmendment
SearchRfqs
```

Query/support endpoints without a strict 1:1 Application class still receive an explicit stable operationId. Physical controller/action names do not define public operation identity.

## 3. DTO sharing

Use this rule:

> Share by semantic identity, not by identical shape.

Shared API-wide enums are appropriate when semantics are truly identical, for example:

```text
RfqStatus
QuoteStatus
QuoteRequestReason
RevisionStatus
```

Do not create controller-local duplicates such as `RfqStatusValue`.

Requests are operation-specific by default. A generic `VersionRequest` is not justified merely because several operations carry one version.

Version properties identify the versioned resource:

```text
ExpectedCurrentVersion
ExpectedDraftVersion
ExpectedWorkingQuoteVersion
ExpectedMemoVersion
```

Do not use bare `ExpectedVersion` at the HTTP boundary when the resource can be named.

## 4. Error contract

Expected HTTP errors use one stable problem contract containing at least:

```text
status
title
detail
code
traceId
```

Calculation failures may additionally expose:

```text
calculationErrorCode
failureLogId
```

Validation may additionally expose field-level errors.

Stable mapping:

```text
Validation           -> 400
Forbidden            -> 403
NotFound             -> 404
InvalidState         -> 409
VersionConflict      -> 409
CalculationFailure   -> 422
ServiceUnavailable   -> 503
unexpected/invariant -> 500 InternalServerError
```

ASP.NET automatic model validation must conform to the same high-level contract.

Expected error responses are declared in OpenAPI; normal 4xx/503 behavior must not be an undocumented runtime surprise.

---

# 04. Multi-item execution policy

A public multi-item operation is appropriate when:

1. the same operation naturally applies to multiple Cases,
2. per-Case partial success is acceptable,
3. there is no cross-Case atomicity/order requirement,
4. the action is not semantically required to remain individual.

Application keeps the single-item primitive. The plural use case orchestrates it:

```text
PresentRfq
PresentRfqs

AssignTrader
AssignTraders

ChangeContactOwner
ChangeContactOwners
```

Do not expose a generic heterogeneous command bus.

## 1. Request shape

One HTTP multi-item endpoint serves both one-item and many-item UI actions.

General rule:

```text
parameters common to the whole action -> request root
parameters varying by Case           -> Items[]
```

Examples:

```text
AssignTradersRequest
  TargetTraderId
  Items[]
    CaseId
    ExpectedCurrentVersion

ConfirmQuotesRequest
  Items[]
    CaseId
    Expiry
    ExpectedCurrentVersion
    ExpectedWorkingQuoteVersion
```

RFQ multi-item route names do not use `bulk`.

## 2. Result semantics

Common per-Case result:

```text
CaseOperationResult
- CaseId
- Status: Applied | NoChange | Failed
- FailureCode?
- Message?
```

Meaning:

- `Applied`: requested change committed.
- `NoChange`: request was valid but effective state already satisfied it.
- `Failed`: that Case could not accept the operation.

`NoChange` is not a generic executor skip.

Expected item failure codes:

```text
VersionConflict
InvalidState
Validation
Forbidden
NotFound
```

System/infrastructure/unexpected failures fail the request boundary rather than being repeated as fake failures for every Case.

Do not add current RFQ projection or `CurrentVersion` to `CaseOperationResult` merely for Web convenience. Authoritative state comes from normal reads.

---

# 05. OpenAPI and generated Web client

The intended Web transport path is:

```text
ASP.NET endpoint/DTO definitions
        ↓
committed OpenAPI
        ↓
generated RTK Query transport client + DTOs/hooks
        ↓
Workspace adapter/composition boundary
        ↓
page-local capability interfaces / Screen
```

Policy:

- use `@rtk-query/codegen-openapi` for the generated RTK Query transport client.
- generate one committed client file initially: `src/Rfq.Web/src/generated/rfqApi.ts`.
- keep a small handwritten `src/Rfq.Web/src/services/baseApi.ts` containing only transport-global setup such as `fetchBaseQuery`, base URL, development identity header propagation, and genuinely global HTTP settings.
- `baseApi` does not contain RFQ endpoints, business transforms, reconciliation policy, SSE, or business cache-invalidation policy.
- generated request/response DTOs, endpoint definitions, and hooks are not handwritten duplicates.
- generated artifacts are committed.
- generation is an explicit command, not a hidden side effect of every normal build.
- generator/tool versions are pinned through the repository package lock/toolchain.
- regeneration must be deterministic enough that rerunning it yields zero diff when the server contract did not change.
- OpenAPI tags remain useful for API classification/documentation, but code generation uses no tag-derived `providesTags` / `invalidatesTags` policy. Generated RTK Query cache invalidation must not become a second business-state synchronization mechanism.
- generated code is never hand-edited or rewritten by a custom post-processing script. If generated output is awkward, first fix the ASP.NET/OpenAPI contract. If the generator shape still cannot be improved cleanly at the server boundary, adapt it at the Workspace boundary.
- do not split the generated client by tag/capability until actual size or ownership pressure justifies it.
- generated DTOs may be consumed directly by Screen when the transport shape and UI meaning are genuinely the same. Do not duplicate DTOs merely to hide that they are generated.

The current intermediate `openapi-typescript` schema-only path and `api-schema.ts` are removed once the generated RTK Query client replaces them.

---

# 06. Frontend transport boundary

Keep the behavioral boundary:

```text
generated API client
        ↓
Workspace
        ↓
page-local capability interfaces / Screen
```

Workspace absorbs:

- generated hook invocation
- `unwrap()`
- transport request construction
- API argument/route naming
- mutation reconciliation
- top-level transport/API failure normalization
- response-to-UI adaptation where required

Screen remains independent of RTK Query transport behavior, but it may use generated request/response data types directly when their meaning and lifecycle already match the UI concept. Introduce page-local types only when the UI concept genuinely differs.

RTK Query-specific failure shapes such as `FetchBaseQueryError` / `SerializedError` do not cross the Workspace boundary. When the server returns the stable problem contract, Workspace exposes `ApiProblemDetails`; unknown network/parse failures are reduced to a generic transport failure appropriate to the page capability. Do not create another global `OperationError` layer merely to rename `ApiProblemDetails`.

`CaseOperationResult.Status == Failed` is an ordinary HTTP-200 business item result. It is not transport/API error normalization.

Handwritten SSE remains outside OpenAPI/RTK Query generation. Put EventSource lifecycle, development identity query propagation, category parsing, reconnect/close behavior, and callback delivery in a small transport module such as `services/worklistStream.ts`. `App` interprets those wake-up categories and updates the corresponding change versions.

Do not create wrapper layers that merely forward generated calls without adapting anything.

---

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
  - process-local BusinessDateRfqSnapshot + refresh coordinator
  - relevant-subscriber SSE wake-up endpoint
  - Business Date / snapshot BackgroundService
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

Operational/read-model tuning values use typed host/infrastructure configuration rather than scattered constants. Initial defaults include:

- Business Date poll interval: 10 minutes
- snapshot refresh coalescing: 500 ms
- snapshot refresh retry count/delay: configurable bounded values
- Past RFQ search result cap: 20,000
- Recent Revisions default limit: 50 (existing API maximum remains 100 unless deliberately changed)

These are operational/product tuning values, not Domain constants.

---

## 2. OpenAPI integration

ASP.NET endpoint/DTO definitions are authoritative. Detailed OpenAPI, operationId, DTO, error-contract, generated-client, and drift-check policy is defined in sections 3-6 of this document.

---

## 3. Current-Business-Date read snapshot

Sales/Trader current worklists use a bounded, process-local immutable snapshot:

```text
BusinessDateRfqSnapshot
- BusinessDate
- Generation
- Sales projections
- Trader projections
- routing metadata needed for audience diff/filtering
```

The snapshot is built by a dedicated Infrastructure loader from PostgreSQL and atomically swapped only after a complete successful build.

Reads:

- Application resolves the current Business Date and passes it explicitly to the Sales/Trader query port
- when snapshot Business Date/generation is current, query implementations filter in memory and return immediately
- when the snapshot is dirty, a GET participates in/awaits the same single-flight refresh; it must not return the older generation as current data
- if the requested Business Date and runtime snapshot disagree, re-check the authoritative DB Business Date and converge the runtime snapshot; a request that crossed Business-Date rollover may resolve/retry once rather than serving the wrong day

Refresh/invalidation:

```text
successful RFQ/business commit
-> generation++
-> signal refresh coordinator
-> optional short coalescing window
-> one DB rebuild for the latest generation
-> atomic immutable swap
-> diff old/new snapshot
-> notify relevant local SSE subscribers
```

If generation advances from 10 to 20 before refresh starts, build once for the latest state rather than processing ten queued refresh jobs.

If generation advances while a rebuild is running, the in-progress build must not be treated as final. Re-run/converge until the published snapshot corresponds to the latest observed generation. Intermediate stale builds may be retained internally but must not be served as current.

The initial refresh-coalescing value is approximately **500 ms** and is configuration, not business semantics.

## 4. Snapshot failure and retry behavior

Snapshot refresh failure is operationally critical because authoritative Sales/Trader reads depend on a fresh snapshot.

Rules:

- retain the last successfully built immutable snapshot for diagnostics/recovery, but do not serve it as current when the coordinator knows a newer generation/Business Date is required
- current-worklist GET returns a service-unavailable failure rather than silently returning the stale snapshot
- snapshot refresh performs a bounded configurable retry count for transient failures/contention; retry settings live in host/infrastructure configuration rather than Domain/Application
- after bounded retries are exhausted, the snapshot remains dirty/unavailable and later background retry continues
- startup does not fail the whole process solely because the initial snapshot cannot be built; the process starts with the read model unavailable, reports the incident, exposes unhealthy/degraded readiness, and retries until recovered
- normal shutdown cancellation is not an incident

Background refresh/startup failures are reported through the existing Host-level `IIncidentReporter`; Infrastructure does not depend directly on the API incident abstraction. GET-triggered failures propagate through the normal API error boundary so the same incident mechanism can report them without duplicate/storm reporting.

## 5. Business Date authority and rollover

The database Business Date value is the source of truth.

The runtime keeps only a derived current-Business-Date mirror for the snapshot.

- startup reads the authoritative DB Business Date and attempts the initial snapshot build
- a low-frequency background poll checks the authoritative Business Date; initial interval is **10 minutes**
- ordinary Application/business use cases continue to resolve Business Date through the authoritative provider
- a query-side Business-Date mismatch triggers an immediate authoritative re-check, so correctness does not depend on waiting for the next 10-minute poll
- on detected rollover, build the new-Business-Date snapshot first and atomically swap only after success
- if rollover build fails, retain the old snapshot object but do not present it as the new current Business Date; current-day reads remain unavailable until the new snapshot succeeds
- successful rollover wakes relevant clients with a Business-Date-changed signal so they re-read Business Date and page state

Do not infer rollover from system clock/calendar rules.

## 6. Realtime change notification and SSE routing

Persisted semantic Events and realtime invalidation are deliberately separate.

Realtime flow:

```text
business mutation commit
-> process-local generation invalidation
-> latest snapshot rebuild
-> diff old/new snapshot
-> determine affected Sales users / Trader desks / invalidation categories
-> wake only relevant process-local SSE subscribers
-> FE performs authoritative page GET
```

The old design where every SSE connection polls the global persisted Event cursor is not the target architecture.

Subscriber routing is process-local and may retain:

- current user identity
- desk
- relevant role/page context
- coarse invalidation interests

Audience is derived from both old and new snapshot routing metadata so responsibility changes do not lose the previous audience. For example, Contact Owner or Assigned Trader changes may require waking both old and new audiences.

Useful coarse invalidation categories include Sales list, Trader list, Recent Revisions stale, and Business Date changed. These are wake-up categories only; SSE must not become row-patch authority.

Slow subscribers must be coalesced/bounded (for example capacity 1 / “change pending”) rather than receiving unbounded queues.

SSE reconnect must cause authoritative catch-up so a transient connection loss cannot permanently hide a committed change. Persisted Event replay is not required for current-worklist correctness.

Same-user multiple tabs are independent subscribers; do not suppress a wake-up merely because the mutation actor has the same user ID.

## 7. Persisted Events remain separate

Persisted `RfqEvent` / `QuoteEvent` records remain semantic audit/business-history data and may support history/revision-aware query surfaces.

There is no requirement for a public persisted-Event feed endpoint. Current-worklist correctness and reconnect catch-up must not depend on semantic Event coverage.

## 8. Expiry BackgroundService

Run an ASP.NET Core `BackgroundService` in the same App Server process.

Initial interval:

```text
10 seconds
```

Worker finds current quoted items with:

```text
ExpiresAt <= now
```

and runs the normal `ExpireQuote` application use case, which invokes the Domain `QuoteTransitions.Expire` transition.

Do not implement expiry as ad-hoc SQL state mutation disconnected from Domain/Application rules.

---

## 9. Expiry idempotency and concurrency

Initial deployment assumes one App Server, but ExpireQuote should be safe if attempted more than once.

Use typed state and `StateVersion` preconditions so only a still-current confirmed quote can transition.

If a human action already Withdrawn/Closed/Revised the RFQ, expiry should do nothing or return a harmless no-op/conflict outcome according to the use-case contract.

Future multi-instance coordination is deferred.

---

## 10. Logging, audit, observability

Keep these concerns separate.

### Domain/business audit

Use persisted:

- RfqEvent
- QuoteEvent

### Business calculation failure

Use CalculationFailureLog for expected calculation-engine failures where required by workflow/audit.

### Technical logging

Use `ILogger<T>` for ordinary technical diagnostics.

### Unexpected incidents

Unexpected API/background-worker failures are reported through a Host-level `IIncidentReporter` abstraction.

The reporter does not classify exceptions. Classification belongs to the caller/boundary.

Initial implementation may simply log, but the boundary allows later integration with Sentry/Application Insights/OpenTelemetry/internal alerting without moving policy into Domain/Application.

Incident reporting is best-effort:

- reporter failure must not replace the original HTTP error
- reporter failure must not terminate worker processing solely because alert delivery failed
- avoid duplicate `LogError` between caller and reporter

API unexpected errors:

- return generic 500 detail
- include `ProblemDetails.extensions.traceId`
- report the same trace ID in the Incident
- include concise operation identity such as HTTP method + path
- do not include full request bodies or sensitive RFQ content by default

### Background workers

Normal shutdown cancellation is not an incident.

Unexpected worker exceptions are reported through `IIncidentReporter`.

Snapshot/Business-Date worker failures use the same incident boundary. Repeated retry failures for the same unavailable period should not create an unbounded incident storm; report the operational failure coherently and report/log recovery separately as appropriate.

Business-state races that the use case already classifies as expected must not be reintroduced as broad swallowed `InvalidOperationException` catches.

### Tracing / metrics

Use standard .NET primitives such as `ActivitySource` and `Meter`.

---

## 11. Event retention

Initial implementation does not delete Events by count or age.

Retain events; archive/partition later only if actual growth/compliance requires it.

---

## 12. Realtime subscriber filtering

Do not wake every connected client for every mutation.

Server-side old/new snapshot diff and subscriber metadata decide which Sales users / Trader desks / coarse surfaces are affected. Filtering occurs before delivery; do not broadcast identity-rich changes to all clients and rely on frontend filtering.

---

## 13. Authentication scope

The environment identifies current user and roles somehow.

Exact token/header/session mechanism is not part of this design.

Application consumes `CurrentUser`; transport does not drive Domain design.


---



# 08. Testing and Seed Data

## 1. Domain unit tests

Focus on typed state and transition invariants.

At minimum cover:

- initial Draft -> Active/Open confirm
- Active -> Presented -> Active
- Open -> Cancelled -> Active/Reopened
- Open -> Closed Hit/Away
- `QuoteRequested` / `QuoteConfirmed` construction and transitions
- invalid state combinations cannot be constructed through public APIs
- Quote Confirm coherently returns updated RFQ + immutable ConfirmedQuote
- Quote Confirm validates WorkingQuote/current Revision association
- Withdraw rejects Presented
- Expire from Active and Presented
- Amendment Save/Confirm/Discard
- old current Revision -> Superseded on amendment Confirm
- WorkingQuote transition behavior
- memo transitions
- ownership PickUp/Release/Assign/TakeOver
- `StateVersion` validation / checked Next
- representative Domain exception categories

Test Domain transitions as pure business operations without repository/clock dependencies.

---

## 2. Application/use-case tests

Use repository mocks/fakes where appropriate.

Focus on:

- correct Domain transition/factory invoked
- central authorization invoked
- IDs/time/business date allocated outside Domain
- initial Confirm creates WorkingQuote and persists atomically
- amendment Confirm creates/seeds a new WorkingQuote for the new Revision
- Quote Confirm allocates QuoteId outside Domain and persists both outputs
- calculation success revalidates state after external calculation
- calculation failure leaves WorkingQuote unchanged
- multi-item partial-success behavior
- `CreateFromExisting` desk/business-date rule, including UTC/JST boundary regression

Application tests should use typed IDs/state rather than string status comparisons.

---

## 3. Infrastructure integration tests

Use **real PostgreSQL**, preferably Testcontainers.

Focus on:

- EF mappings/rehydration of typed lifecycle state
- migrations from the existing schema
- transaction atomicity
- one Draft partial unique index
- one WorkingQuote per Revision
- optimistic concurrency / `StateVersion` mapping
- Category/Security/Routing FKs
- JSONB event payloads
- quote-event joins after removal of `QuoteEvent.CaseId`
- expiry worker selection/transition behavior

Do not use EF InMemory/SQLite as a PostgreSQL substitute.

### Event identity/order tests

Current workflows do not depend on EventId as a global commit-order cursor, so no reverse-commit ordering test is required merely to preserve the retired Event-feed contract.

When the later ID-model review decides Event identity/allocation semantics, add PostgreSQL integration tests for the semantics that are actually chosen. If a future dedicated delta cursor requires commit-visible ordering, test that cursor with real PostgreSQL and the actual allocation mechanism.

---

---

## 4. API tests

Cover representative:

- happy paths
- 400 validation
- 403 authorization
- 404
- 409 version conflict
- calculation failure mapping
- existing external DTO/JSON compatibility after internal typed refactor
- OpenAPI smoke test

---

## 5. FE tests

FE tests cover page behavior, transport-adapter behavior, Live/Paused reconciliation, editing safety, and generated/handwritten boundary integration as applicable. Internal backend refactors should not force FE changes unless the HTTP/application boundary intentionally changes.

---

## 6. E2E journeys

Keep representative journeys:

1. Sales creates RFQ -> Trader quotes -> Sales presents -> Hit
2. Sales revises RFQ -> Trader requotes -> Away
3. Quote expires -> Requested/Expired -> requote
4. Cancel -> Reopen -> requote
5. WorkingQuote / Revision concurrency conflict

---

## 7. Master/seed strategy

Keep realistic but mock/seed master data.

### Category

Category is master data, not enum.

Seed a small set such as JGB / Corporate / Other with:

- stable CategoryId
- display Name
- CategoryRouting to default trader

Security rows reference CategoryId by FK.

### Other master data

Retain current pragmatic User/Desk/Client/Security seeds.

Security data should remain realistic enough to exercise search.

---

## 8. Demo RFQ seed

Generate enough RFQs to exercise:

- Draft
- Requested
- Quoted
- Presented
- Cancelled
- Hit
- Away
- Revised history
- Expired/Withdrawn events
- multiple Contact Owners / Assigned Traders / ownership states
- Past RFQ search

Seed data must be constructible through the new valid Domain model or equivalent trusted persistence setup; do not seed impossible field combinations that Domain could not rehydrate.

---

## 9. Security seed from realistic Japanese bond data

Where practical, use public Japanese bond reference-price/security-list data as a realistic source for demo/search seed.

Useful extracted fields include:

- bond/security code
- name
- coupon
- maturity

Generate application `SecurityId` and add mock fields where needed:

- ISIN
- ticker / BBG-style display
- CategoryId

The goal is realistic search/demo behavior, not a legally authoritative security master.

---

## 10. Mock ISIN

For demo seed, structurally valid JP-prefixed ISIN-like identifiers with valid check digits may be generated where convenient.

They do not need to reproduce an issue's actual ISIN.

Important test properties:

- full 12-character lookup
- prefix lookup
- alphanumeric support
- checksum path if implemented

---

## 11. Calculation mock data

Do not build fake holiday, curve, convention, or yield-spread master systems inside the RFQ app.

Hide them behind MockCalculationClient.

The mock may use one deterministic formula across arbitrary securities and should produce plausible price/yield/simple-yield/spread/accrued/settlement/delta values plus controlled errors.


---



# 09. Generated artifacts and tooling

Generated contract artifacts are committed.

The server OpenAPI artifact remains:

```text
artifacts/openapi/Rfq.Api.json
```

After generated RTK Query migration, the Web contract artifact is:

```text
src/Rfq.Web/src/generated/rfqApi.ts
```

`npm run generate:api` is the canonical local generation flow:

```text
dotnet build / OpenAPI generation
→ RTK Query OpenAPI code generation
→ formatter
```

Generation must be explicit, reproducible, and deterministic enough that rerunning the canonical command with an unchanged server contract yields zero unexpected diff.

Do not add a separate drift-check command now. There is no CI pipeline yet, and a meaningful drift check must regenerate before comparing. When CI is introduced, add generate + diff verification there.

The intermediate `openapi-typescript` schema generation and `src/Rfq.Web/src/generated/api-schema.ts` are removed once no consumer remains.

---

# 10. Engineering decisions

## 1. Current-Business-Date worklists use one immutable process-local snapshot

A dedicated Infrastructure loader builds Sales and Trader projections for the current Business Date. Query implementations filter those projections in memory by user/desk.

Mutation commits increment/invalidate generation cheaply; refresh is single-flight/coalesced and converges to the latest state rather than queuing one refresh per mutation.

PostgreSQL remains authoritative.

## 2. Database Business Date is authoritative

Polling is proactive detection only. Query mismatch and business use cases re-check authoritative DB Business Date when correctness requires it.

Do not infer desk Business Date from system clock.

## 3. Horizontal scaling requires cross-process invalidation

Snapshot generation and SSE subscriptions are process-local in the current single-API-process deployment.

If horizontal scaling is introduced, add reliable cross-process invalidation/notification (for example PostgreSQL LISTEN/NOTIFY or another explicit mechanism). Sticky routing alone is not sufficient.

## 4. Persisted semantic Events and realtime invalidation are separate

Persisted Events represent business/audit history. Snapshot generation/diff + relevant SSE wake-up represents realtime current-state discovery.

Do not manufacture semantic Events merely to refresh another browser session.

## 5. Generated transport is not a second state-synchronization policy

OpenAPI drives endpoint/DTO/hook generation, but generated RTK Query tags do not own RFQ freshness. Current-state reconciliation remains explicit at the Workspace/read-model boundary.

## 6. Generated data shapes may cross the Workspace boundary; transport failures may not

Generated DTOs may reach Screen unchanged when UI and API semantics match. RTK Query-specific invocation/error mechanics remain behind Workspace.

## 7. SSE remains handwritten transport

EventSource lifecycle is intentionally separate from the generated HTTP client. SSE carries wake-up categories only; authoritative state still comes from normal queries.
