# 06 — Domain State Typing, Immutable Value Semantics, and Bulk Operations

## Goal

Apply the cross-cutting Domain/Application cleanup deferred from 05.

This phase has three goals:

1. make Hit/Away explicit lifecycle types rather than payload values on a generic closed state;
2. align immutable Domain state/value types with record semantics without turning entities into value objects;
3. redesign bulk operations as thin orchestration over the corresponding single-item use cases, with explicit partial-success semantics and safe Unit of Work cleanup.

Semantic baseline:

```text
ac6359ad5a2e1614b3eb904d79a68f57a9a9c818
```

Apply the small 05 follow-up fixes separately if they have not already been applied. Do not fold unrelated 05 cleanup into this phase.

Backward compatibility is not required. The development database may be reset if a persistence representation change genuinely requires it.

Do not broaden this phase into unrelated Domain/API/UI redesign.

---

## 1. Lifecycle: replace generic Closed(Hit/Away) with typed states

The current lifecycle shape is conceptually:

```text
RfqLifecycle
├─ DraftRfq
├─ OpenRfq
│  ├─ ActiveRfq
│  └─ PresentedRfq
├─ CancelledRfq
└─ ClosedRfq
   ClosedQuoteId
   Outcome = RfqStatus.Hit | RfqStatus.Away
```

Replace it with:

```text
RfqLifecycle
├─ DraftRfq
├─ OpenRfq
│  ├─ ActiveRfq
│  └─ PresentedRfq
├─ CancelledRfq
└─ ClosedRfq (abstract)
   ├─ HitRfq
   └─ AwayRfq
```

`ClosedRfq` owns the common closed-state data, including `ClosedQuoteId`.

`HitRfq` and `AwayRfq` are concrete lifecycle states.

`CancelledRfq` remains a sibling of `ClosedRfq`.

Do not make Cancelled a Closed subtype: Cancelled remains reopenable and has different business semantics.

Persistence may continue to flatten lifecycle into the existing status columns if that remains convenient. Do not force the persistence model to mirror the Domain inheritance hierarchy.

---

## 2. Lifecycle transitions must become operation-specific

Remove Domain transition APIs that accept `RfqStatus outcome` as an input payload.

Use explicit transitions conceptually equivalent to:

```text
CloseHit(OpenRfq)      -> HitRfq
CloseAway(OpenRfq)     -> AwayRfq

CorrectToHit(AwayRfq)  -> HitRfq
CorrectToAway(HitRfq)  -> AwayRfq
```

The Application layer must likewise stop exposing generic `CloseRfq(outcome)` / `CorrectRfqOutcome(outcome)` commands.

Replace them with explicit use cases:

```text
CloseHitRfq
CloseAwayRfq
CorrectOutcomeToHit
CorrectOutcomeToAway
```

The corresponding HTTP contract should be explicit:

```text
POST /api/rfqs/{caseId}/close/hit
POST /api/rfqs/{caseId}/close/away

POST /api/rfqs/{caseId}/outcome/correct-to-hit
POST /api/rfqs/{caseId}/outcome/correct-to-away
```

Do not keep a public API enum whose only purpose is selecting Hit vs Away for these commands.

Keep API response models API-owned as established in 05.

---

## 3. Preserve cancellation/reopen semantics

Cancellation semantics remain:

```text
Open -> Cancelled
```

Cancel must retain:

- Assigned Trader
- Contact Owner
- Working Quote
- Confirmed Quote history
- pending amendment Draft, if one exists

The pending amendment Draft must NOT be discarded on Cancel.

Reopen remains conceptually:

```text
CancelledRfq
  -> ActiveRfq(
       QuoteRequested(Reopened),
       Ownership = Unowned)
```

Reopen must not resurrect an old confirmed quote.

If a pending amendment Draft existed before Cancel, it remains pending after Reopen.

Add tests that make this behavior explicit.

---

## 4. Record/class semantics

Use the following criterion:

```text
entity / versioned business object
    -> class

identity-less immutable Domain state/value
    -> explicit-property record

simple result carrier
    -> positional record is acceptable
```

The purpose is semantic correctness, not terseness.

Do not mechanically convert every immutable class to a positional record.

---

## 5. Convert immutable state/value families to explicit-property records

Convert the following Domain concepts to record semantics where they are currently ordinary classes:

```text
RfqLifecycle hierarchy
ActiveQuoteState hierarchy
QuoteExpiry hierarchy
RevisionTerms
QuoteConfirmation
CalculatedQuotePayload
ManualQuotePayload
```

`Ownership` is already a record hierarchy; keep that model.

Use explicit-property records with constructors / factory methods that preserve validation and invariants.

Avoid positional records for Domain state/value types that have invariants or should not expose a broad `with { ... }` mutation surface merely for convenience.

Existing typed IDs that are already appropriate record/value types should remain so.

Transition/result DTO-like carriers may remain positional records.

---

## 6. Keep entities/business objects as classes

Do NOT convert these to value records:

```text
RfqCase
RfqRevision
WorkingQuote
ConfirmedQuote
SalesMemo
TraderMemo
```

Rationale:

- `RfqCase` has stable CaseId identity across lifecycle/version changes.
- `RfqRevision` is a revision/entity with identity and lifecycle.
- `WorkingQuote` is a versioned business object tied to a Revision with audit metadata and transitions.
- `ConfirmedQuote` has QuoteId identity and immutable history semantics.
- Sales/Trader Memo are independently versioned business objects.

Do not use default structural record equality for these entities.

---

## 7. Bulk design: operation-specific public use cases

Do not introduce a public generic bulk executor such as:

```text
BulkExecute<T>
BulkCommand<T>
GenericBulkRunner
```

Public Application bulk use cases must remain business-operation-specific.

An internal helper may share repetitive loop/result/exception-mapping code, but it must remain an implementation detail.

The core orchestration shape is intentionally simple:

```csharp
foreach (var item in items)
{
    try
    {
        var result = await single.ExecuteAsync(...);
        // map single-item result to Succeeded / Skipped
    }
    catch (recoverable expected exception)
    {
        // reset uncommitted state
        // map to Failed
    }
}
```

Do not duplicate the single-item business transition logic inside bulk use cases.

In particular, replace the current `BulkCloseRfqs` implementation that reimplements close logic.

---

## 8. Bulk transaction semantics

A bulk HTTP request is NOT one atomic business transaction.

Required semantics:

```text
one HTTP request
    -> item 1: independent business transaction
    -> item 2: independent business transaction
    -> item 3: independent business transaction
    ...
```

Each successful single-item use case commits independently through the existing `IUnitOfWork.SaveChangesAsync()`.

Partial success is expected.

Do not wrap the entire bulk request in one transaction.

Do not send N HTTP requests from the browser to simulate bulk behavior.

Initial execution is sequential.

Do not parallelize bulk items in this phase.

EF `DbContext` remains scoped as today; do not introduce a fresh DI scope/DbContext per item unless the existing scoped UoW cannot satisfy the cleanup contract below.

---

## 9. Add Unit of Work discard/reset capability

Extend the Application Unit of Work contract:

```csharp
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    void DiscardChanges();
}
```

`DiscardChanges()` means:

> discard uncommitted in-memory persistence/event state so the same scoped Unit of Work can safely continue with the next independent bulk item.

For the current EF implementation it must at minimum reset:

```text
DbContext tracked pending changes
PersistedEventSink pending events
```

The natural EF implementation is expected to include the equivalent of:

```csharp
dbContext.ChangeTracker.Clear();
eventSink.Clear();
```

Do not expose EF types through the Application interface.

Any transaction opened inside `SaveChangesAsync()` must already be disposed/rolled back on failure as appropriate.

The important requirement is that a failed item must not leak:

- tracked entity modifications
- stale tracked state
- pending events

into a later successful item.

Bulk orchestration must call `DiscardChanges()` after a recoverable item failure before proceeding to the next item.

Do not redesign all use cases around `IUnitOfWork.ExecuteAsync(Func<...>)` in this phase.

Keep the existing explicit `SaveChangesAsync()` pattern.

---

## 10. Single-item use cases remain the source of business semantics

Bulk orchestration must not inspect the database separately and reproduce state-machine rules merely to decide whether something should be skipped.

For use cases that support bulk, enrich the corresponding single-item use-case result where necessary so a benign no-op can be represented explicitly.

Conceptually:

```text
single use case

actual state change
    -> explicit success result

benign "already in desired/non-actionable state"
    -> explicit operation-specific no-op result

expected business failure
    -> exception

unexpected/fatal failure
    -> exception
```

Do NOT introduce a generic Application-wide `Result<TError, TValue>` refactor.

Language/runtime/infrastructure exceptions still exist and bulk must still use try/catch.

Use operation-specific return types where needed.

Example:

```text
WithdrawQuote
    Withdrawn
    AlreadyRequested
```

The exact type names may follow existing project naming conventions.

---

## 11. Skipped vs Failed

Bulk item status is a common three-way concept:

```text
Succeeded
Skipped
Failed
```

Meaning:

### Succeeded

The requested business change was applied and committed.

### Skipped

The single-item operation explicitly reports a benign no-op / already-satisfied condition.

Example already agreed:

```text
Bulk WithdrawQuote
    Quote already Requested
        -> Skipped
```

Another important lifecycle example:

```text
Bulk CloseAway
    already Away
        -> Skipped

    already Hit
        -> Failed
```

because changing Hit to Away is an outcome correction, not an idempotent CloseAway.

Do not infer Skipped from exception message text.

Delete patterns such as:

```csharp
ex.Message.Contains("Presented")
```

for control flow.

### Failed

An expected/recoverable exception prevented that item from completing.

The bulk operation records the failure and continues after calling `IUnitOfWork.DiscardChanges()`.

---

## 12. Bulk item result contract

Use a common semantic result shape equivalent to:

```text
CaseId
Status      // Succeeded | Skipped | Failed
Code?       // stable machine-readable code
Message?    // human-readable detail
```

Application and API contracts must remain separate as established in 05; map Application status/code to API-owned models.

Do not use arbitrary strings such as:

```text
"Confirmed"
"Discarded"
"Withdrawn"
"Failed"
"Skipped"
```

as the machine-readable status contract.

A human-readable message may still describe the operation-specific result.

`Code` is stable/machine-readable.

`Message` is human-readable and may change.

Do not make callers parse `Message`.

---

## 13. Recoverable exception mapping

Bulk must distinguish recoverable item failures from failures that should abort the whole bulk request.

Expected/recoverable examples include the existing business/API-style failures:

```text
StateVersionMismatchException
DomainRuleViolationException
DomainValidationException
UnauthorizedAccessException
KeyNotFoundException
ArgumentException
InvalidOperationException
```

Map them to stable failure codes rather than exposing exception type names as the contract.

Use a small common mapping where practical, e.g. concepts equivalent to:

```text
VersionConflict
InvalidState
Validation
Forbidden
NotFound
```

The exact constant/type organization may follow the project style.

The human-readable `Message` may reuse an expected business exception message when it is intended for the operator.

Do not make exception message text part of branching logic.

---

## 14. Fatal exceptions abort the bulk request

Do not convert every exception into an item-level `Failed`.

At minimum, these categories must abort the bulk operation and propagate normally:

```text
OperationCanceledException / request cancellation
DomainInvariantException
unexpected programming exceptions
infrastructure failures that indicate the operation cannot safely continue
```

A general database connectivity failure, for example, should not produce hundreds of item-level failures while continuing against a broken persistence dependency.

Only explicitly recognized recoverable business/concurrency failures should become `Failed`.

---

## 15. Bulk-enabled operations for this phase

Implement public bulk support for the following operations:

### Initial Draft

```text
ConfirmInitialDraft
DiscardInitialDraft
```

### Amendment

```text
ConfirmAmendment
DiscardAmendment
```

### Quote

```text
ConfirmQuote
WithdrawQuote
```

### Presentation

```text
PresentQuote
UnpresentQuote
```

### Ownership

```text
PickUpRfq
ReleaseRfq
AssignTrader
```

### Lifecycle

```text
CloseAwayRfq
CancelRfq
```

Each must have an operation-specific bulk Application use case, conceptually:

```text
BulkConfirmInitialDrafts
BulkDiscardInitialDrafts

BulkConfirmAmendments
BulkDiscardAmendments

BulkConfirmQuotes
BulkWithdrawQuotes

BulkPresentQuotes
BulkUnpresentQuotes

BulkPickUpRfqs
BulkReleaseRfqs
BulkAssignTrader

BulkCloseAwayRfqs
BulkCancelRfqs
```

Names may be adjusted slightly for grammar/project conventions, but preserve operation specificity.

---

## 16. Operations explicitly NOT bulk-enabled now

Do not add bulk support for:

```text
CreateDraft
CreateFromExisting
UpdateInitialDraft
SaveAmendment
CalculateWorkingQuote
UpdateManualWorkingQuote
TakeOverRfq
CloseHitRfq
CorrectOutcomeToHit
CorrectOutcomeToAway
ReopenRfq
UpdateSalesMemo
UpdateTraderMemo
```

Specific reasons already decided:

- `CalculateWorkingQuote`: RFQs may use different calculation/reference-rate logic by maturity/security; a user-driven same-action bulk recalc is not a sound generic operation.
- `TakeOverRfq`: strong/exceptional forced ownership transfer; normal flow can use Release + PickUp.
- `CloseHitRfq`: Hit is economically significant and may later connect to booking; do not provide bulk Hit now.
- outcome correction: exceptional individual correction, not a bulk workflow.
- memo bulk update: potentially useful but deliberately deferred.

---

## 17. Bulk operations explicitly deferred

Do not add bulk support in 06 for:

```text
ChangeWorkingQuoteMode
ChangeContactOwner
```

Both have plausible bulk workflows:

- Working Quote mode switch during calculation-service failure
- Contact Owner reassignment during handoff

but they are intentionally deferred rather than required in this phase.

Do not remove or weaken the single-item operations.

---

## 18. Cancel vs Away

Preserve the semantic distinction:

```text
Away
    RFQ was quoted/presented and did not result in a trade

Cancel
    RFQ is withdrawn/terminated without that Away outcome
```

The detailed future business taxonomy may evolve, but Bulk Cancel is required in 06.

Do not collapse Cancel into Away.

Do not make Cancel a Closed subtype.

---

## 19. API organization for bulk

Keep bulk endpoints inside the feature that owns the corresponding business operation.

Do not create a global generic:

```text
/api/bulk
BulkController
```

Existing feature-local routes may be normalized as part of this phase because backward compatibility is not required.

Use explicit action names and API-owned request/response models.

Examples of acceptable shape:

```text
POST /api/rfqs/drafts/bulk-confirm
POST /api/rfqs/drafts/bulk-discard

POST /api/rfqs/amendment/bulk-confirm
POST /api/rfqs/amendment/bulk-discard

POST /api/rfqs/quotes/bulk-confirm
POST /api/rfqs/quotes/bulk-withdraw

POST /api/rfqs/bulk-present
POST /api/rfqs/bulk-unpresent

POST /api/rfqs/ownership/bulk-pick-up
POST /api/rfqs/ownership/bulk-release
POST /api/rfqs/ownership/bulk-assign-trader

POST /api/rfqs/bulk-close-away
POST /api/rfqs/bulk-cancel
```

These route strings are guidance for consistency, not a requirement to create a separate cross-feature controller.

Prefer feature-local controllers and contracts.

Do not expose a generic outcome parameter for bulk Close.

There is `BulkCloseAway`, not `BulkClose(outcome)`.

---

## 20. AssignTrader bulk input

`BulkAssignTrader` applies one target Assigned Trader to the selected RFQs.

The target trader belongs to the bulk command/request, while each item carries its CaseId and expected version.

Conceptually:

```text
targetAssignedTraderId
items[]
    caseId
    expectedCurrentVersion
```

Continue to perform authoritative server-side trader eligibility validation.

Do not trust candidate endpoints.

If an item is already assigned to the target trader and the single-item semantics classify that as a benign no-op, return Skipped rather than manufacturing an error.

---

## 21. ConfirmQuote bulk input

Bulk ConfirmQuote still represents an explicit Trader decision.

Do not auto-confirm quotes merely because they are calculated.

Each item must provide the concurrency/input data required by the single ConfirmQuote operation, including the relevant expected versions and expiry policy.

If the UX supplies a common expiry to all selected items, the API may place that common policy at bulk-request level; if current UI semantics require per-item expiry, keep it per item.

Do not weaken ConfirmQuote validation to make bulk easier.

---

## 22. No bulk business-logic duplication

A bulk use case should call the corresponding single use case.

Examples:

```text
BulkConfirmAmendments
    -> ConfirmAmendment.ExecuteAsync for each item

BulkWithdrawQuotes
    -> WithdrawQuote.ExecuteAsync for each item

BulkCloseAwayRfqs
    -> CloseAwayRfq.ExecuteAsync for each item
```

Do not repeat:

- authorization
- lifecycle transitions
- repository update logic
- event recording
- working quote changes
- revision handling

inside the bulk use case.

Internal shared iteration/result mapping is acceptable after the operation-specific use case boundary.

---

## 23. Event behavior

Successful bulk items must produce exactly the same semantic events as invoking the corresponding single-item use case individually.

Skipped items must not create mutation events.

Failed items must not leak pending events into later items.

This is one of the required tests for `IUnitOfWork.DiscardChanges()`.

Do not introduce special aggregate `Bulk...` Domain events merely because multiple commands arrived in one HTTP request.

---

## 24. Frontend

Update the existing React UI/API client for the changed lifecycle endpoints and bulk contracts.

For screens that already support row selection / bulk actions, expose the required 06 bulk operations where they naturally belong.

Do not create a large new workflow or generic bulk-action framework solely for this phase.

Do not simulate bulk by firing one HTTP request per selected row.

The frontend sends one bulk request and renders per-item:

```text
Succeeded
Skipped
Failed
```

with operator-readable failure/skip messages where useful.

For features that do not currently have an appropriate screen/selection UX, the backend/API contract may be implemented without inventing an unrelated new page.

---

## 25. Tests: lifecycle typing

Add/update Domain/Application tests for at least:

```text
Open -> HitRfq
Open -> AwayRfq

AwayRfq -> HitRfq correction
HitRfq -> AwayRfq correction

CancelledRfq remains distinct/reopenable

Cancel retains pending amendment Draft
Reopen retains pending amendment Draft
Reopen does not resurrect an old confirmed quote
```

Verify that close/correction APIs no longer require an `RfqStatus outcome` parameter.

---

## 26. Tests: record semantics

Update tests as needed for the record conversion.

Verify that invariants remain enforced for:

```text
QuoteExpiry.After duration
RevisionTerms
QuoteConfirmation
quote payloads
lifecycle state construction
```

Do not write tests that rely on entity structural equality for:

```text
RfqCase
RfqRevision
WorkingQuote
ConfirmedQuote
SalesMemo
TraderMemo
```

---

## 27. Tests: Unit of Work cleanup

Add a focused persistence test proving the failure-isolation requirement.

At minimum cover a scenario equivalent to:

```text
bulk item A
    mutates tracked entity
    records pending event
    SaveChanges fails with concurrency conflict

Bulk catches recoverable conflict
    IUnitOfWork.DiscardChanges()

bulk item B
    executes successfully
    commits
```

Verify:

- A's tracked mutation is not committed with B
- A's pending event is not committed with B
- B can execute successfully using the same scoped UoW/DbContext
- the concurrency failure is still surfaced as item Failed

Also test `DiscardChanges()` itself where appropriate.

---

## 28. Tests: bulk semantics

For representative bulk operations, cover:

```text
all success
mixed success / skipped / failed
recoverable failure followed by success
fatal exception aborts the whole request
request cancellation aborts the whole request
```

Explicitly test:

```text
WithdrawQuote already Requested -> Skipped
CloseAway already Away          -> Skipped
CloseAway already Hit           -> Failed
```

Verify that no bulk implementation depends on exception-message substring matching.

Verify one successful item corresponds to one single-use-case commit.

Do not require an all-or-nothing transaction.

---

## 29. Existing bulk implementations to replace/clean up

The 05 baseline currently contains:

```text
BulkConfirmAmendments
BulkDiscardAmendments
BulkWithdrawQuotes
BulkCloseRfqs
```

Refactor them into the 06 model.

In particular:

- keep confirm/discard amendment as operation-specific bulk use cases;
- replace arbitrary string result contracts;
- remove message-based Skipped detection from `BulkWithdrawQuotes`;
- replace generic `BulkCloseRfqs(outcome)` with `BulkCloseAwayRfqs`;
- `BulkCloseAwayRfqs` must delegate to `CloseAwayRfq`;
- do not add `BulkCloseHitRfqs`.

---

## 30. Scope boundaries

Do not use 06 to redesign:

- authentication
- candidate search
- category routing
- Business Date
- event persistence format
- quote calculation model
- EOD
- repository architecture generally
- generic command bus / MediatR pipeline
- global Result/Option error framework
- DI scope-per-item infrastructure
- parallel bulk execution
- retry policies
- memo workflows
- booking integration

Do not introduce a generic transaction runner merely because bulk exists.

Keep the current Application `IUnitOfWork.SaveChangesAsync()` model and add the minimal cleanup capability required for safe partial-success bulk execution.

---

## 31. Completion criteria

06 is complete when:

1. Domain lifecycle has explicit `HitRfq` / `AwayRfq` states.
2. Application/API expose explicit Hit/Away close/correction commands rather than an outcome enum input.
3. Cancel/Reopen semantics, including pending amendment retention, are preserved.
4. identity-less immutable Domain state/value types use appropriate explicit-property record semantics.
5. entity/versioned business objects remain classes.
6. bulk public use cases are operation-specific and delegate to single-item use cases.
7. the required 13 bulk operations are implemented.
8. explicitly excluded/deferred bulk operations have not been added.
9. bulk results use typed `Succeeded / Skipped / Failed` semantics with stable codes/messages.
10. no bulk branching depends on exception message text.
11. successful items commit independently.
12. recoverable failures call `IUnitOfWork.DiscardChanges()` and do not contaminate later items/events.
13. fatal/cancellation exceptions abort the bulk request.
14. existing API/frontend consumers are updated for the changed lifecycle/bulk contracts.
15. the full backend test suite passes.
16. the existing frontend test/build commands pass.

Keep the implementation focused on these requirements.
