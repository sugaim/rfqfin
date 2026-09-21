# 05 — API Boundary and Application Contract Cleanup

## Goal

Refactor the HTTP API around business/use-case features rather than the current controller buckets, and fix Application/Domain contract problems exposed by that API review.

This phase may modify Domain/Application contracts when the API review has shown that the existing semantic model is wrong.

Do not broaden the work into unrelated Domain redesign.

Backward compatibility is not required. The development database may be reset.

---

## 1. General API structure

Remove the current large horizontal controllers:

- `RfqsController`
- `TraderRfqsController`
- `OperationsController`
- `MastersController`
- `CurrentUserController`
- `RfqDefaultsController`
- `SystemDateController`

Replace them with feature-local API folders/controllers.

Requests, responses and API mappers belong beside the feature that owns them.

Do not create global `Requests/`, `Responses/`, `Mappers/`, `Config/` or similar catch-all folders.

API models must not expose Domain/Application types directly.

At the HTTP boundary:

- typed IDs become primitives
- Domain enums become API-owned enums
- feature-local API mappers convert API contract ↔ Application/Domain
- use concrete/static mappers; do not create mapper interfaces without an actual need

Do not use DTO self-conversion methods such as `From()` or `ToApplication()` going forward.

---

## 2. Error handling

Controller-local business exception handling must be removed.

Use the common middleware/ProblemDetails path.

Expected mapping:

- calculation failure → 422
- unauthorized → 403
- not found → 404
- state version mismatch / domain rule violation / invalid operation → 409
- domain validation / argument validation → 400
- unknown exception → 500

Use a common error contract containing:

- `status`
- `title`
- `detail`
- `code`

Validation errors may additionally contain `errors`.

Use ASP.NET Core request-model validation consistently.

---

## 3. RFQ Drafts

Feature:

```text
RfqDrafts/
```

Own:

- `CreateDraft`
- `CreateFromExisting`
- `UpdateInitialDraft`
- `ConfirmInitialDraft`
- `DiscardInitialDraft`

`CreateFromExisting` belongs here because its result is a new Draft RFQ Case.

Assigned Trader and Settlement Date are required inputs.

```text
AssignedTraderId    required
SettlementDate      required
StandardSettlement  required
Notional            nullable
Message             empty allowed
```

Remove nullable/fallback semantics from normal create/update flow.

Special server-side `CreateFromExisting` rules may still derive values where that use case itself owns the behavior.

---

## 4. RFQ Creation Context

Replace `RfqDefaults` with:

```text
GET /api/rfqs/creation-context?securityId=...
```

Application concept:

```text
ResolveRfqCreationContext
RfqCreationContext
```

Response:

```text
categoryId
categoryName
defaultAssignedTraderId
defaultAssignedTraderName
standardSettlementDate
```

Do not return:

```text
securityId
businessDate
contactOwnerId
contactOwnerName
```

This endpoint provides context/suggestions for initializing the form.

It is not an implicit fallback mechanism for `CreateDraft`.

The backend must still resolve/revalidate authoritative values during creation.

---

## 5. RFQ Amendments

Feature:

```text
RfqAmendments/
```

Own:

- `SaveAmendment`
- `ConfirmAmendment`
- `DiscardAmendment`
- existing bulk amendment operations

Routes:

```text
PUT  /api/rfqs/{caseId}/amendment
POST /api/rfqs/{caseId}/amendment/confirm
POST /api/rfqs/{caseId}/amendment/discard
```

`SaveAmendment` intentionally behaves as an upsert of the pending Draft Revision:

- no pending Draft → create one
- pending Draft exists → update it

Confirm continues to require a pending Draft Revision.

Do not redesign bulk semantics in 05.

---

## 6. Working Quotes

Working Quote is the editable pre-confirmation quote object and gets its own feature:

```text
WorkingQuotes/
```

Own:

- `CalculateWorkingQuote`
- `ChangeWorkingQuoteMode`
- `UpdateManualWorkingQuote`

Routes:

```text
PUT /api/rfqs/{caseId}/working-quote/calculate
PUT /api/rfqs/{caseId}/working-quote/mode
PUT /api/rfqs/{caseId}/working-quote/manual
```

Keep RFQ Case version and Working Quote version as separate concurrency checks.

---

## 7. RFQ Quotes

Feature:

```text
RfqQuotes/
```

Own:

- `ConfirmQuote`
- `WithdrawQuote`
- `GetRfqQuotes`

Routes:

```text
POST /api/rfqs/{caseId}/quote/confirm
POST /api/rfqs/{caseId}/quote/withdraw
GET  /api/rfqs/{caseId}/quotes
```

`GetRfqQuotes` is the Confirmed Quote history query.

Do not put Present/Unpresent here.

---

## 8. Quote Expiry semantics

Remove nullable-expiry semantics from the business/API contract.

A Quote Expiry always exists and is one of:

```text
None
After(TimeSpan)
```

Do not use `QuoteExpiry.FromMinutes(int?)` where `null` implicitly means `None`.

No-expiry must be explicit.

HTTP contract should use an API-owned tagged union, e.g.:

```json
{ "type": "none" }
```

or:

```json
{
  "type": "after",
  "minutes": 5
}
```

API uses integer minutes as the transport representation.

The mapper converts minutes to `TimeSpan`.

Domain remains:

```text
QuoteExpiry.None
QuoteExpiry.After(TimeSpan)
```

Do not introduce a custom Minutes value type in this phase.

Nullable computed timestamps such as `ExpiresAt` may remain nullable where absence naturally represents no expiration.

---

## 9. RFQ Lifecycle

Feature:

```text
RfqLifecycle/
```

Own:

- `PresentQuote`
- `UnpresentQuote`
- `CloseRfq`
- `CorrectRfqOutcome`
- `CancelRfq`
- `ReopenRfq`

Present/Unpresent belong here because Domain semantics are lifecycle transitions:

```text
ActiveRfq(QuoteConfirmed)
    -> PresentedRfq

PresentedRfq
    -> ActiveRfq(QuoteConfirmed)
```

Routes may remain simple RFQ lifecycle commands:

```text
POST /api/rfqs/{caseId}/present
POST /api/rfqs/{caseId}/unpresent
POST /api/rfqs/{caseId}/close
POST /api/rfqs/{caseId}/correct-outcome
POST /api/rfqs/{caseId}/cancel
POST /api/rfqs/{caseId}/reopen
```

### Close / Cancelled in 05

Keep the current lifecycle shape in 05:

```text
CancelledRfq
ClosedRfq(outcome = Hit | Away)
```

`CancelledRfq` remains distinct from `ClosedRfq` because it is temporarily terminal and may be reopened.

Current cancellation semantics remain:

- Open → Cancelled
- Assigned Trader retained
- Contact Owner retained
- Working Quote retained
- pending amendment Draft retained
- Confirmed Quote history retained

Reopen does not resurrect an old confirmed quote:

```text
CancelledRfq
  -> ActiveRfq(
       QuoteRequested(Reopened),
       Ownership = Unowned)
```

`Close` remains Hit/Away only and requires a current confirmed quote.

Outcome correction remains:

```text
Closed(Hit) <-> Closed(Away)
```

Do not redesign the Hit/Away type hierarchy in 05.

That is explicitly deferred to 06.

---

## 10. RFQ Ownership

Feature:

```text
RfqOwnership/
```

Own:

- `PickUpRfq`
- `ReleaseRfq`
- `AssignTrader`
- `TakeOverRfq`

These are state-changing business operations, not Trader-screen controller actions.

Example routes:

```text
POST /api/rfqs/{caseId}/ownership/pick-up
POST /api/rfqs/{caseId}/ownership/release
POST /api/rfqs/{caseId}/ownership/take-over
PUT  /api/rfqs/{caseId}/assigned-trader
```

Keep Assigned Trader terminology distinct from runtime Ownership.

Do not redesign bulk behavior here.

---

## 11. RFQ Responsibility

Feature:

```text
RfqResponsibility/
```

Own:

```text
ChangeContactOwner
```

Route:

```text
PUT /api/rfqs/{caseId}/contact-owner
```

Contact Owner is distinct from Trader Ownership.

Restrict Contact Owner handoff to Open RFQs.

Do not solve rare post-close/post-cancel override workflows in this phase.

Those authorization exceptions are deferred.

---

## 12. Sales Memo / Trader Memo Domain correction

The existing `CaseMemo` model is semantically wrong.

Sales Memo and Trader Memo are independent business data and must have independent concurrency/versioning.

Replace the Domain concept:

```text
CaseMemo
  SalesMemo
  TraderMemo
  Version
```

with independent concepts:

```text
SalesMemo
  CaseId
  Value
  Version

TraderMemo
  CaseId
  Value
  Version
```

Application use cases remain:

- `UpdateSalesMemo`
- `UpdateTraderMemo`

Each checks and increments only its own version.

API feature may remain:

```text
RfqMemos/
```

Routes:

```text
PUT /api/rfqs/{caseId}/memos/sales
PUT /api/rfqs/{caseId}/memos/trader
```

Do not return a generic `CaseMemoResponse`.

Persistence may use either:

- separate tables, or
- one physical table with separate memo/version columns

but Sales Memo and Trader Memo must be independent concurrency units.

No compatibility with the old schema is required.

---

## 13. RFQ Revisions

Revision history is a separate business concept from the Amendment workflow.

Feature:

```text
RfqRevisions/
```

Route:

```text
GET /api/rfqs/{caseId}/revisions
```

Split the existing combined history query abstraction into focused queries:

```text
IRfqRevisionQueries
IRfqQuoteQueries
```

with corresponding EF implementations.

Do not create a generic `History` or `Timeline` feature.

---

## 14. Sales and Trader read models

Sales and Trader active RFQ lists are different use-case projections.

Do not force them through one generic Active RFQ DTO/query.

Use separate features/read models:

```text
SalesRfqs/
TraderRfqs/
```

Routes:

```text
GET /api/sales-rfqs
GET /api/trader-rfqs
```

Application query contracts should likewise be distinct, e.g.:

```text
ISalesRfqQueries
ITraderRfqQueries
```

Each projection should expose only fields required by that use case.

---

## 15. RFQ Search

The existing `PastRfqs` query is not actually limited to past RFQs.

Rename the concept to RFQ Search.

Feature:

```text
RfqSearch/
```

Application:

```text
IRfqSearchQueries
RfqSearch
RfqSearchResult
RfqSearchItem
```

Infrastructure:

```text
EfCoreRfqSearchQueries
```

Route:

```text
GET /api/rfqs/search
```

Current filters remain supported:

```text
createdFrom
createdTo
clientId
securityId
categoryId
contactOwnerId
salesId
assignedTraderId
status
caseId
```

Rename `From` / `To` to `createdFrom` / `createdTo` because the filter is based on `CreatedAt`.

Preserve the current result cap / narrowing behavior.

---

## 16. Current actor: Me

Replace `CurrentUserController` with:

```text
Me/
```

Route:

```text
GET /api/me
```

Response:

```text
userId
roles
deskId
```

Do not return Quote Expiry settings from `/me`.

Remove `DefaultQuoteExpiryMinutes` from generic user summaries.

`/me` represents how the application recognizes the current request actor, not authentication/login management.

---

## 17. Personal settings under Me

Keep user-owned settings underneath the `Me` feature.

### Quote Expiry Setting

Routes:

```text
GET /api/me/settings/quote-expiry
PUT /api/me/settings/quote-expiry
```

The setting always exists.

It uses the same semantic tagged expiry model as quote confirmation:

```text
None
After(minutes)
```

Do not represent absence of the setting as `null`.

### Grid Config

Routes:

```text
GET /api/me/grid-configs/{screenId}/{configKey}
PUT /api/me/grid-configs/{screenId}/{configKey}
```

The API contract carries actual JSON:

```json
{
  "version": 1,
  "config": {
    "columns": []
  }
}
```

Use `JsonElement` at the API boundary.

Application/Infrastructure must not interpret the config payload.

Application may store it as an opaque string.

Rename Application `ConfigJson` to `Config` if practical so the persistence representation does not leak upward.

Do not introduce a wrapper abstraction purely to call the content “opaque”.

---

## 18. Business Date

Replace `SystemDate` terminology with Business Date.

Application abstraction:

```text
IBusinessDateProvider
```

for the current trading business date.

Likely method:

```text
GetCurrentAsync()
```

Route:

```text
GET /api/business-date
```

Response:

```text
date
```

This application remains Tokyo-only for now.

Do not introduce `DeskId` into the Business Date API/schema preemptively.

The separate timestamp → desk-local calendar-date abstraction should be renamed to:

```text
IDeskLocalDateResolver
```

Rename semantic variables from `systemDate` to `businessDate`.

Do not overdesign multi-location support.

---

## 19. Category Routing

Only Category → Default Assigned Trader is currently editable.

Do not add editable Security → Category routing.

Future Security → DefaultBook → Category resolution is outside this phase.

`ICategoryRouting` must return a required trader:

```csharp
Task<UserId> GetDefaultAssignedTraderAsync(...)
```

Missing routing is a configuration error, not normal absence.

Routes:

```text
GET /api/category-routings
PUT /api/category-routings/{categoryId}
```

Suggested response fields:

```text
categoryId
categoryName
defaultTraderId
defaultTraderName
```

Default trader validation must remain authoritative server-side.

---

## 20. Candidate APIs

Remove `MastersController`.

Do not expose `IUserDirectory`, `ISecuritySearch` or `IClientSearch` directly as generic master APIs.

Expose requirement-oriented candidate queries.

Routes:

```text
GET /api/assignable-traders
GET /api/contact-owner-candidates
GET /api/rfqs/candidates/clients?q=...
GET /api/rfqs/candidates/securities?q=...
```

`assignable-traders` means users currently selectable as Assigned Trader, including same-desk/role rules.

`contact-owner-candidates` means users currently eligible for Contact Owner handoff, currently same desk and Sales/Trader.

Commands must revalidate targets; candidate endpoints are UI support only.

Client/Security endpoints are RFQ input candidates, not generic master-data endpoints.

---

## 21. Events API

Keep:

```text
GET /api/events
GET /api/events/stream
```

`GET /api/events` must expose an API-owned typed event union.

Do not expose generic arbitrary payload JSON.

Use stable explicit discriminators and event-specific fields.

Example:

```json
{
  "type": "rfqClosedHit",
  "eventId": 123,
  "caseId": 456,
  "quoteId": "..."
}
```

Map:

```text
Application semantic event
  -> EventsApiMapper
  -> API event DTO union
```

SSE remains only a change/wake-up signal.

Do not stream the entire typed event payload through SSE.

---

## 22. Pricer

Remove Pricer from `OperationsController`.

Feature:

```text
Pricer/
```

Route:

```text
POST /api/pricer
```

Use API-owned request/response models and API-owned CalculationDriver enum.

`ScratchPricer` may continue returning Domain/Application calculation payload internally.

Do not create an extra Application DTO layer solely for HTTP mapping.

---

## 23. EOD

Do not redesign EOD requirements in 05.

The current EOD behavior is deliberately deferred for later discussion.

Only extract it from `OperationsController` as necessary to remove that controller.

Preserve current behavior.

Do not infer a new EOD permission model, drill-down model, or workflow in this phase.

A temporary dedicated controller is acceptable if needed.

---

## 24. Bulk operations

Bulk behavior is explicitly deferred to 06 for cross-cutting review.

05 must not redesign which commands support bulk or the generic bulk execution model.

Preserve existing bulk behavior while relocating APIs/features as necessary.

06 will review:

- which operations should support bulk
- single-use-case repetition vs dedicated bulk orchestration
- partial-success semantics
- transaction boundaries
- endpoint naming and placement

---

## 25. Explicit 06 follow-up items

Do not implement these in 05.

### Hit/Away lifecycle typing

Review whether:

```text
ClosedRfq(outcome = Hit | Away)
```

should become:

```text
abstract ClosedRfq
  HitRfq
  AwayRfq
```

so Domain lifecycle state no longer needs `RfqStatus` as an input payload.

Also review transition shape:

```text
CloseHit(OpenRfq)      -> HitRfq
CloseAway(OpenRfq)     -> AwayRfq
CorrectToHit(AwayRfq)  -> HitRfq
CorrectToAway(HitRfq)  -> AwayRfq
```

`CancelledRfq` should remain conceptually separate because it is reopenable.

### Record hierarchy

Review whether immutable state families should move to record hierarchies:

```text
RfqLifecycle
Ownership
ActiveQuoteState
```

while keeping `RfqCase` as an entity/class.

Do not mix this representation refactor into 05.

### Bulk semantics

Perform the cross-cutting bulk review described above.

---

## 26. Frontend

Update the React frontend to the new API contracts/routes.

In particular:

- `/me` no longer contains default expiry
- quote expiry uses explicit tagged policy
- RFQ creation uses `RfqCreationContext`
- assigned trader and settlement date are required
- BusinessDate replaces SystemDate terminology
- Grid Config is transported as JSON rather than escaped JSON text
- Sales and Trader RFQ projections remain separate
- Search uses `createdFrom` / `createdTo`
- new candidate routes replace generic master routes

Do not introduce unrelated UI redesign.

Do not add a new API-codegen framework in this phase unless already required by the project baseline.

---

## 27. Testing

After the refactor, run the full existing test suite.

Add/update tests for semantic changes, especially:

- QuoteExpiry `None` / `After`
- required AssignedTrader / SettlementDate
- required Category routing
- BusinessDate naming/behavior
- SalesMemo and TraderMemo independent versions
- ChangeContactOwner rejected for non-Open RFQ
- typed event API mapping
- API-owned enum/DTO mapping
- RFQ Search `createdFrom` / `createdTo`
- Grid Config JSON transport
- new route contracts
- Sales/Trader projections remain independent

Do not add CI.

---

## Suggested commit boundaries

### Commit 1 — API feature structure

Split controllers/features and remove the horizontal controller buckets.

Avoid mixing large semantic Domain changes into this commit unless required to keep the build compiling.

### Commit 2 — API/Application contract cleanup

Implement:

- Me/settings
- BusinessDate
- CreationContext
- candidate APIs
- CategoryRouting
- required create/update fields
- QuoteExpiry explicit semantics

### Commit 3 — RFQ semantic corrections

Implement:

- SalesMemo / TraderMemo split
- ChangeContactOwner Open-only rule
- Lifecycle/Quote/WorkingQuote feature alignment
- revision/quote query split

### Commit 4 — Read models / Events / frontend / regression tests

Implement:

- SalesRfqs / TraderRfqs
- RfqSearch
- typed Events API
- Pricer extraction
- frontend migration
- full test cleanup

The exact commit count may vary slightly to keep every commit buildable, but keep semantic changes independently reviewable.

---

## Non-goals

Do not:

- redesign EOD
- redesign bulk semantics; that is 06
- split `ClosedRfq` into Hit/Away types yet; that is 06
- convert lifecycle state classes to records yet; that is 06
- implement multi-desk BusinessDate
- design Security → DefaultBook → Category administration
- introduce generic repositories or another ORM layer
- expose Domain/Application objects directly over HTTP
- preserve old API routes solely for compatibility
- preserve old development database schema solely for compatibility
- add speculative abstraction layers

The result of 05 should be a feature-oriented HTTP boundary whose contracts reflect the actual business semantics already established in Domain/Application, with the semantic inconsistencies discovered during review corrected rather than hidden behind DTO mapping.
