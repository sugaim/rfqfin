# Codex Instruction — 03a: Application/API Boundary and Contract Cleanup

## Position of this task

Repository: `sugaim/rfqfin`

Required baseline:

- 02b implementation: `c8af2e3d09f76d75ea6830e6859a06cf6673c195`
- 02b merged/reviewed baseline: `d36550cf00c36c66c31e1dc6f55e8f6cfc830f1e`
- canonical documentation split completed:
  - `docs/engineering.md`: `9bb178d7b2fbd0256ddaedbcc95ac2fac5dd6ab1`
  - `docs/design.md`: `06c9283bf2389629fe414652f2ee0d8436ec2347`

Start from current `main` at or after `06c9283bf2389629fe414652f2ee0d8436ec2347`.

Before changing code, read both active canonical documents:

- `docs/design.md`
- `docs/engineering.md`

Their responsibility split is intentional:

- `design.md` owns business meaning, invariants, workflow, authorization, UX semantics, persistence/business facts, and durable architecture.
- `engineering.md` owns source organization, HTTP/OpenAPI policy, multi-item execution shape, generated-client policy, runtime/read-model mechanics, testing/tooling, and deployment assumptions.

Files under `docs/refactoring/` are historical implementation instructions. Do not treat them as active authority and do not delete them in this task.

This instruction is the concrete implementation delta for 03a. Where this instruction is more specific than the canonical documents, it specializes those documents; it does not override their principles.

---

# 0. 02b review result and required follow-up

02b is accepted. No blocking redesign is required.

The reviewed implementation correctly covers the important 02b semantics:

- persisted Business-Date membership
- immutable current-Business-Date snapshot
- generation-based invalidation
- single-flight dirty GET refresh
- convergence when writes happen during refresh
- post-commit invalidation
- unavailable state instead of known-stale reads
- old/new audience diff for SSE routing
- independent same-user-tab wake-ups
- authoritative Live/Paused reconciliation
- mutation-success vs reconciliation-read-failure separation
- Create From Existing reconciliation
- zero-difference Amendment behavior
- autosave conflict stopping/recovery behavior

Two follow-ups are small enough to include in 03a.

## 0.1 Development identity propagation for SSE

Normal RTK Query requests currently send:

```text
X-Development-User
```

from local storage.

Browser `EventSource` cannot set that custom header, so the current SSE request may resolve as the configured/default `sales-dev` even when the Web is operating as `trader-a` or another selected development identity.

Fix this explicitly.

Required behavior:

1. normal API requests continue to use `X-Development-User`
2. `DevelopmentCurrentUser` accepts one dedicated development-only query parameter as fallback when that header is absent
3. the final SSE endpoint includes the selected development identity through that query parameter
4. header wins over query parameter
5. both paths use the same existing identity allow-list/validation
6. this remains clearly development authentication plumbing; do not generalize it into production auth design

Add a regression test proving Trader SSE subscription resolves to the Trader identity/role rather than the default Sales identity.

The final SSE route is defined later in this instruction.

## 0.2 Shared snapshot refresh lifetime cancellation

Preserve the important current behavior:

- cancellation of one HTTP request waiting for a shared refresh must not cancel the refresh for other callers

But remove the current `CancellationToken.None` lifetime leak:

- the coordinator owns a lifetime cancellation source, or equivalent host-lifetime cancellation
- disposal/application shutdown cancels the underlying snapshot DB load/retry delay
- request cancellation only stops that caller waiting
- normal shutdown cancellation is not reported as an incident
- do not create one refresh task per request

Add focused tests for:

- one waiter cancelling while another still receives the shared refresh result
- coordinator/host lifetime cancellation cancelling the underlying load/retry

Do not otherwise redesign 02b.

---

# 1. Scope of 03a

03a stabilizes the Application/API contract before generated RTK Query migration.

03a includes:

1. Application source ownership cleanup
2. API source ownership cleanup
3. removal of top-level technical `Bulk` and `Queries` organization
4. public plural multi-item Application use cases
5. missing multi-item use cases
6. private HTTP route cleanup
7. explicit stable OpenAPI `operationId`
8. API DTO/shared-enum/version naming cleanup
9. common multi-item result semantics
10. uniform HTTP error contract and OpenAPI error declarations
11. deletion of dead private API verticals
12. handwritten Web transport adaptation to the new contract
13. the two 02b follow-ups above
14. regenerated committed OpenAPI/schema artifacts

03a does **not** include:

- `@rtk-query/codegen-openapi` migration
- generated RTK Query hooks/client adoption
- CI pipeline setup
- horizontal scaling / LISTEN-NOTIFY
- booking
- new business workflows
- broad persistence refactoring unrelated to moved/deleted ports

Generated-client migration is 03b.

---

# 2. Application source organization

Implement the ownership structure already defined in `docs/engineering.md`.

Target:

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

- delete the top-level `Bulk/`
- delete the top-level `Queries/`
- capability-specific ports live beside the capability
- `Abstractions/` is only for genuinely cross-capability technical ports
- `Common/` is only for genuine cross-use-case execution/result machinery
- do not create a new miscellaneous bucket

## 2.1 Correct transition ownership

Rename/rehome:

```text
PresentQuote   -> PresentRfq
UnpresentQuote -> UnpresentRfq
```

under:

```text
UseCases/Rfqs/Lifecycle/
```

Move:

```text
WithdrawQuote
ExpireQuote
```

under:

```text
UseCases/Quotes/Lifecycle/
```

Do not preserve the old public type names as compatibility aliases.

## 2.2 Worklists

Move Sales/Trader current-worklist use cases, models, and capability-specific query ports under:

```text
UseCases/Worklists/Sales/
UseCases/Worklists/Trader/
```

This includes the current 02b `ISalesRfqQueries` / `ITraderRfqQueries` contracts.

Keep the 02b cached Infrastructure implementations; only adjust their references/namespaces as needed.

## 2.3 Other ownership

Move/rehome without redesigning semantics:

```text
Search           -> UseCases/Search/
Post Process     -> UseCases/PostProcess/
Grid settings    -> UseCases/Settings/Grid/
typed settings   -> UseCases/Settings/
reference/candidates -> UseCases/ReferenceData/
scratch pricing  -> UseCases/Pricing/
```

Use `ScratchPrice` as the public Application capability name.

---

# 3. Multi-item Application use cases

Remove `Bulk` from public use-case names.

Required plural use cases:

```text
ConfirmInitialDrafts
DiscardInitialDrafts

ConfirmAmendments
DiscardAmendments

ConfirmQuotes
WithdrawQuotes

PresentRfqs
UnpresentRfqs
CloseAwayRfqs
CancelRfqs
ReopenRfqs

PickUpRfqs
ReleaseRfqs
AssignTraders
TakeOverRfqs

ChangeContactOwners
```

The single Application primitive remains for each operation.

New plural use cases required in 03a:

```text
ReopenRfqs
TakeOverRfqs
ChangeContactOwners
```

They must reuse the corresponding single-item business behavior. Do not duplicate authorization, state-transition, or persistence logic.

Post Process remains its own business-specific composite multi-item capability:

```text
CommitPostProcessChanges
```

Do not build a generic heterogeneous command bus.

---

# 4. Multi-item result model

Replace public `Bulk...` result terminology with a common case-operation result.

Conceptually:

```text
CaseOperationResult
- CaseId
- Status
- FailureCode?
- Message?
```

Status:

```text
Applied
NoChange
Failed
```

Semantics:

```text
Applied
  the requested change was committed

NoChange
  the request was valid, but the effective state already satisfied it

Failed
  that Case could not accept the requested operation
```

`NoChange` must not mean an arbitrary executor skip.

Existing operation-specific outcomes map naturally where applicable:

```text
CloseAway:
  ClosedAway       -> Applied
  AlreadyAway      -> NoChange

WithdrawQuote:
  Withdrawn        -> Applied
  AlreadyRequested -> NoChange

AssignTrader:
  Assigned         -> Applied
  AlreadyAssigned  -> NoChange
```

Expected per-Case failure codes:

```text
VersionConflict
InvalidState
Validation
Forbidden
NotFound
```

Do not convert:

```text
ServiceUnavailable
unexpected exception
invariant failure
```

into one fake failed item per Case. Those fail the execution/HTTP boundary.

Do not add current RFQ projection or `CurrentVersion` to `CaseOperationResult` merely for Web state patching.

---

# 5. Multi-item request shape

General rule:

```text
parameters common to the whole action -> request root
parameters varying by Case           -> Items[]
```

Use operation-specific request/item DTOs.

Do not share a generic `VersionRequest` merely because shapes match.

## 5.1 Case/version-only operations

Pattern:

```text
PresentRfqsRequest
  Items[]
    CaseId
    ExpectedCurrentVersion
```

Apply the same pattern where appropriate to:

```text
UnpresentRfqs
CloseAwayRfqs
CancelRfqs
ReopenRfqs
ReleaseRfqs
WithdrawQuotes
```

## 5.2 Assign Traders

```text
AssignTradersRequest
  TargetTraderId
  Items[]
    CaseId
    ExpectedCurrentVersion
```

## 5.3 Pick Up

```text
PickUpRfqsRequest
  Confirmed
  Items[]
    CaseId
    ExpectedCurrentVersion
```

## 5.4 Take Over

```text
TakeOverRfqsRequest
  Confirmed
  Items[]
    CaseId
    ExpectedCurrentVersion
```

## 5.5 Change Contact Owner

```text
ChangeContactOwnersRequest
  TargetContactOwnerId
  Confirmed
  Items[]
    CaseId
    ExpectedCurrentVersion
```

## 5.6 Confirm Quotes

Expiry is per Case:

```text
ConfirmQuotesRequest
  Items[]
    CaseId
    Expiry
    ExpectedCurrentVersion
    ExpectedWorkingQuoteVersion
```

## 5.7 Amendments

Use:

```text
ExpectedCurrentVersion
ExpectedDraftVersion
```

plus operation-specific fields where needed.

## 5.8 Initial Drafts

Use operation-specific item DTOs.

Name the version after the object actually checked. Do not mechanically preserve bare `ExpectedVersion`.

---

# 6. API source organization

Implement the canonical structure from `docs/engineering.md`.

Target:

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

Guidance:

- no top-level catch-all `Controllers/`
- no `Features/`
- no giant `RfqOperationsController`
- `Endpoints/` contains HTTP capabilities
- `Hosting/` contains runtime/process concerns such as workers, incident reporting, development current-user plumbing, and API middleware when appropriate
- `Common/` contains truly shared API contract types

## 6.1 Me

Keep `/api/me/...` routes, but physically split responsibilities:

```text
MeController
QuoteSettingsController
ThemeSettingsController
GridConfigsController
```

Do not create one giant settings controller.

---

# 7. Shared API contract policy

Use:

> semantic shared types are common; operation requests are use-case-specific; shape equality alone does not justify sharing.

## 7.1 Shared enums

Create one API transport enum per genuinely shared semantic concept, for example:

```text
RfqStatus
QuoteStatus
QuoteRequestReason
RevisionStatus
```

Remove duplicates such as:

```text
RfqStatusValue
QuoteStatusValue
```

Do not use `Value` solely to distinguish transport enums.

## 7.2 Version names

At the HTTP boundary use semantic names:

```text
ExpectedCurrentVersion
ExpectedDraftVersion
ExpectedWorkingQuoteVersion
ExpectedMemoVersion
```

Avoid bare `ExpectedVersion` where the versioned resource is known.

Internal Domain/Application parameters do not need mechanical renaming when their context is already unambiguous.

---

# 8. Delete dead private API verticals

The API is private to this repo/Web for 03a. Do not preserve dead compatibility routes.

## 8.1 EOD — delete completely

Delete:

- EOD HTTP endpoint/contracts
- Application EOD query/DTO
- Infrastructure implementation
- DI
- Web hook/type residue
- EOD-only tests

Do not delete unrelated semantic event code merely because a name contains `Eod`.

## 8.2 Category Routing — HTTP only

Delete the Category Routing management HTTP API.

Keep the underlying Application/Infrastructure routing capability used by RFQ creation.

## 8.3 RFQ Revisions query vertical — delete completely

Delete the unused direct revisions endpoint and its dedicated query port/Infrastructure implementation/DI/tests.

Do **not** delete Sales Recent Revisions.

## 8.4 Persisted Event feed HTTP — delete

Delete the old persisted Event feed GET API and feed-only plumbing if it has no remaining internal consumer:

```text
GET /api/events
IEventFeed
EfCoreEventFeed
event-feed API response hierarchy
Web persisted-event residue
```

Keep:

- persisted semantic Event storage
- audit/history use
- 02b realtime invalidation/SSE capability

The SSE capability moves to Worklists.

---

# 9. Final HTTP routes and exact operationIds

Every HTTP operation must have an explicit, stable, unique OpenAPI `operationId`.

Do not let generator/controller/action naming infer it.

For Application-backed operations, the operationId is the cleaned public Application use-case name.

## 9.1 RFQ single-only operations

| Method | Route | operationId |
|---|---|---|
| POST | `/api/rfqs/drafts` | `CreateDraft` |
| POST | `/api/rfqs/drafts/confirm` | `ConfirmNewRfq` |
| PUT | `/api/rfqs/{caseId}/draft` | `UpdateInitialDraft` |
| POST | `/api/rfqs/{caseId}/create-from-existing` | `CreateFromExisting` |
| POST | `/api/rfqs/{caseId}/amendment/start` | `StartAmendment` |
| PUT | `/api/rfqs/{caseId}/amendment` | `SaveAmendment` |
| POST | `/api/rfqs/{caseId}/close/hit` | `CloseHitRfq` |
| POST | `/api/rfqs/{caseId}/outcome/correct-to-hit` | `CorrectOutcomeToHit` |
| POST | `/api/rfqs/{caseId}/outcome/correct-to-away` | `CorrectOutcomeToAway` |
| PUT | `/api/rfqs/{caseId}/memos/sales` | `UpdateSalesMemo` |
| PUT | `/api/rfqs/{caseId}/memos/trader` | `UpdateTraderMemo` |
| PUT | `/api/rfqs/{caseId}/working-quote/calculate` | `CalculateWorkingQuote` |
| PUT | `/api/rfqs/{caseId}/working-quote/mode` | `ChangeWorkingQuoteMode` |
| PUT | `/api/rfqs/{caseId}/working-quote/manual` | `UpdateManualWorkingQuote` |
| GET | `/api/rfqs/{caseId}/quotes` | `GetRfqQuoteHistory` |
| GET | `/api/rfqs/creation-context` | `ResolveRfqCreationContext` |

Do not keep separate single HTTP endpoints for operations that have a plural endpoint below. The single Application primitive remains.

## 9.2 RFQ multi-item operations

| Method | Route | operationId |
|---|---|---|
| POST | `/api/rfqs/confirm-initial-drafts` | `ConfirmInitialDrafts` |
| POST | `/api/rfqs/discard-initial-drafts` | `DiscardInitialDrafts` |
| POST | `/api/rfqs/confirm-amendments` | `ConfirmAmendments` |
| POST | `/api/rfqs/discard-amendments` | `DiscardAmendments` |
| POST | `/api/rfqs/confirm-quotes` | `ConfirmQuotes` |
| POST | `/api/rfqs/withdraw-quotes` | `WithdrawQuotes` |
| POST | `/api/rfqs/present` | `PresentRfqs` |
| POST | `/api/rfqs/unpresent` | `UnpresentRfqs` |
| POST | `/api/rfqs/close-away` | `CloseAwayRfqs` |
| POST | `/api/rfqs/cancel` | `CancelRfqs` |
| POST | `/api/rfqs/reopen` | `ReopenRfqs` |
| POST | `/api/rfqs/pick-up` | `PickUpRfqs` |
| POST | `/api/rfqs/release` | `ReleaseRfqs` |
| POST | `/api/rfqs/assign-trader` | `AssignTraders` |
| POST | `/api/rfqs/take-over` | `TakeOverRfqs` |
| POST | `/api/rfqs/change-contact-owner` | `ChangeContactOwners` |

No RFQ multi-item route contains `bulk`.

## 9.3 Worklists

| Method | Route | operationId |
|---|---|---|
| GET | `/api/worklists/sales` | `GetActiveSalesRfqs` |
| GET | `/api/worklists/sales/recent-revisions` | `GetSalesRecentRevisions` |
| GET | `/api/worklists/trader` | `GetActiveTraderRfqs` |
| GET | `/api/worklists/stream` | `StreamWorklistInvalidations` |

The stream remains SSE wake-up only.

## 9.4 Search

| Method | Route | operationId |
|---|---|---|
| GET | `/api/search/rfqs` | `SearchRfqs` |

## 9.5 Reference data

| Method | Route | operationId |
|---|---|---|
| GET | `/api/reference-data/assignable-traders` | `GetAssignableTraders` |
| GET | `/api/reference-data/contact-owner-candidates` | `GetContactOwnerCandidates` |
| GET | `/api/reference-data/clients` | `SearchClients` |
| GET | `/api/reference-data/securities` | `SearchSecurities` |

## 9.6 Post Process

| Method | Route | operationId |
|---|---|---|
| GET | `/api/post-process` | `GetPostProcess` |
| POST | `/api/post-process/commit` | `CommitPostProcessChanges` |

## 9.7 Me/settings

| Method | Route | operationId |
|---|---|---|
| GET | `/api/me` | `GetMe` |
| GET | `/api/me/settings/quote-expiry` | `GetQuoteExpiry` |
| PUT | `/api/me/settings/quote-expiry` | `SaveQuoteExpiry` |
| GET | `/api/me/settings/default-quote-mode` | `GetDefaultQuoteMode` |
| PUT | `/api/me/settings/default-quote-mode` | `SaveDefaultQuoteMode` |
| GET | `/api/me/settings/theme` | `GetTheme` |
| PUT | `/api/me/settings/theme` | `SaveTheme` |
| GET | `/api/me/grid-configs/{screenId}/{configKey}` | `GetGridConfig` |
| PUT | `/api/me/grid-configs/{screenId}/{configKey}` | `SaveGridConfig` |

## 9.8 Business Date

| Method | Route | operationId |
|---|---|---|
| GET | `/api/business-date` | `GetBusinessDate` |

## 9.9 Pricing

| Method | Route | operationId |
|---|---|---|
| POST | `/api/pricing/scratch` | `ScratchPrice` |

## 9.10 System

| Method | Route | operationId |
|---|---|---|
| GET | `/api/system/health` | `GetHealth` |
| GET | `/api/system/readiness` | `GetReadiness` |

Business Date does not move under System.

---

# 10. Removed legacy routes

The generated OpenAPI must no longer contain:

```text
GET /api/events
/api/eod
/api/category-routings
/api/rfqs/{caseId}/revisions
```

It must not contain `bulk` in RFQ multi-item routes.

Do not keep route aliases.

---

# 11. HTTP error contract

Keep the semantic error taxonomy from `docs/design.md` and implement the transport policy from `docs/engineering.md`.

Stable mapping:

| Meaning | HTTP | code |
|---|---:|---|
| request validation | 400 | `Validation` |
| forbidden | 403 | `Forbidden` |
| not found | 404 | `NotFound` |
| invalid business state | 409 | `InvalidState` |
| optimistic concurrency | 409 | `VersionConflict` |
| calculation failure | 422 | `CalculationFailure` |
| read model unavailable | 503 | `ServiceUnavailable` |
| unexpected/invariant | 500 | `InternalServerError` |

Define one explicit API problem schema, conceptually:

```text
ApiProblemDetails
- Status
- Title
- Detail
- Code
- TraceId
- Errors?
- CalculationErrorCode?
- FailureLogId?
```

Use `application/problem+json`.

ASP.NET automatic model validation must produce the same high-level contract:

```text
code = Validation
detail
traceId
```

Field-level validation errors may be added.

Multi-item scope:

```text
invalid request envelope/body
-> top-level HTTP 400

expected Case-specific failure
-> HTTP 200 + CaseOperationResult(Failed)

system/unexpected execution failure
-> top-level HTTP failure
```

---

# 12. OpenAPI contract requirements

For every endpoint:

- explicit unique `operationId`
- explicit success response schema
- correct request-body/query schema
- correct nullable semantics
- shared semantic enum schemas
- expected error response schemas/statuses

At minimum, normal 400/403/404/409/422/503 outcomes must be declared where the endpoint can actually produce them.

Add an automated contract check that verifies:

1. every operation has an operationId
2. operationIds are unique
3. required operationIds from section 9 exist
4. dead routes from section 10 are absent
5. no RFQ multi-item route contains `bulk`
6. common problem schema exists
7. duplicated same-semantic status enums are not regenerated per controller

Do not rely only on visual inspection of `Rfq.Api.json`.

---

# 13. Web adaptation in 03a

Do **not** migrate to generated RTK Query yet.

Keep the handwritten transport temporarily, but adapt it to the stable 03a contract.

Required:

- update routes
- update request shapes
- update semantic version field names
- rename public `Bulk...` transport operations/types to plural operation names
- use `items: [singleItem]` for a single UI action when the separate single HTTP endpoint was removed
- consume `CaseOperationResult`
- handle `Applied | NoChange | Failed`
- preserve Workspace -> page-local capability -> Screen boundary
- preserve authoritative reconciliation after mutation
- preserve mutation-success vs reconciliation-read-failure distinction
- do not patch RFQ business state from mutation responses
- do not add wrapper layers that merely forward transport calls

Update App-level SSE from the old route to:

```text
/api/worklists/stream
```

and apply the development identity fix from section 0.

Preserve categories:

```text
sales-list
trader-list
recent-revisions
business-date
```

Do not restore persisted Event polling.

---

# 14. Infrastructure changes allowed

Limit Infrastructure changes to:

- moved Application contracts/namespaces
- dead vertical deletion
- new plural use-case DI
- 02b lifetime-cancellation follow-up
- API/OpenAPI support that requires boundary wiring

Do not use 03a for a broad EF/persistence refactor.

Keep the 02b snapshot architecture recognizable.

---

# 15. Tests required

## 15.1 02b follow-up

Add tests for:

- Trader development identity over the final SSE endpoint
- header precedence over development identity query fallback
- one request cancelling while shared snapshot refresh continues for another waiter
- coordinator/host lifetime cancellation stopping the underlying load/retry

## 15.2 Multi-item Application

Cover the new:

```text
ReopenRfqs
TakeOverRfqs
ChangeContactOwners
```

Verify:

- single-item business behavior is reused
- per-Case independence
- one expected failure does not roll back previous successful Cases
- version conflict -> item failure
- unexpected/invariant failure aborts instead of becoming ordinary item failure

Cover representative:

```text
Applied
NoChange
Failed
```

including existing no-change outcomes.

## 15.3 API/OpenAPI

Cover representative:

- 400 Validation
- 403 Forbidden
- 404 NotFound
- 409 InvalidState
- 409 VersionConflict
- 422 CalculationFailure
- 503 ServiceUnavailable
- generic safe 500

Add the automated OpenAPI assertions from section 12.

## 15.4 Web

Update/add focused tests for:

- single UI action -> one-item plural request
- Applied result
- NoChange result
- Failed result
- mutation succeeds but reconciliation read fails
- SSE selected development identity
- final worklist/SSE routes

Preserve existing Live/Paused, Amendment, autosave, and Post Process behavior tests.

---

# 16. Generated artifacts

After implementation run:

```text
npm run generate:api
```

03a still commits the current artifacts:

```text
artifacts/openapi/Rfq.Api.json
src/Rfq.Web/src/generated/api-schema.ts
```

Do not introduce `@rtk-query/codegen-openapi` in 03a.

Do not remove `openapi-typescript` yet.

That replacement belongs to 03b.

Regeneration must be stable: a second generation without server changes should produce no unexpected diff.

---

# 17. Validation

Run the strongest repository checks available.

At minimum:

```text
dotnet build
dotnet test
```

From `src/Rfq.Web`:

```text
npm run build
npm run test -- --run
npm run quality
npm run generate:api
```

Then regenerate once more and check for unexpected drift.

If PostgreSQL-backed integration tests cannot run in the environment, report exactly which checks could not run. Do not silently treat them as passed.

---

# 18. Guardrails

Do not:

- rewrite `docs/design.md` / `docs/engineering.md` speculatively during implementation
- add old-route compatibility aliases
- build a generic command bus
- make multi-item operations cross-Case atomic
- add multi-item Hit close
- turn SSE into state authority
- reintroduce Event-feed-driven current-worklist refresh
- add `CurrentVersion` to `CaseOperationResult` for Web convenience
- move every port into `Abstractions`
- centralize request DTOs solely by shape
- create a giant controller
- create a new `Features/` layer
- broadly reorganize Infrastructure
- delete `docs/refactoring/`
- start 03b generated-client migration early

---

# 19. Expected end state

At completion:

```text
Application
- organized by UseCases ownership
- no top-level Bulk/ or Queries/
- transition ownership aligns with Domain
- plural multi-item use cases are colocated with single primitives
- ReopenRfqs / TakeOverRfqs / ChangeContactOwners exist
- CaseOperationResult uses Applied / NoChange / Failed

API
- organized under Endpoints / Hosting / Common
- dead EOD / CategoryRouting HTTP / direct Revisions / persisted Event feed are gone
- private route families are coherent
- RFQ multi-item routes do not say bulk
- every endpoint has explicit stable operationId
- shared semantic enums are not duplicated
- error contract is uniform and represented in OpenAPI

Web
- still handwritten transport in 03a
- adapted to the final 03a contract
- one-item plural requests are used where appropriate
- authoritative reconciliation remains intact
- final SSE route respects selected development identity

02b runtime
- remains generation-based and single-flight
- shutdown can cancel underlying refresh work
- ordinary request cancellation cannot cancel another caller's shared refresh
```

Stop after this state. Do not continue into 03b.
