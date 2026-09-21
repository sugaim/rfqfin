# RFQ Backend Semantic Domain Refactor — Commit 1 Instruction

## 0. Purpose and authority

This is a **single semantic-refactor commit** for the already-implemented RFQ backend.

The purpose is to replace the current mutable / primitive-heavy backend model with the agreed domain model while preserving current business behavior, API behavior, persistence behavior, and existing completed functionality except for the explicit correctness fixes in this instruction.

Use these sources in this order of authority:

1. the updated `docs/rfq-design-spec/` package supplied with this instruction
2. this instruction
3. existing repository implementation instructions
4. current implementation

If an older implementation instruction conflicts with the updated canonical design, the **updated canonical design wins**.

Base the work on the current `main` implementation. Do not reimplement the project from scratch.

This is **backend only**. Do not refactor `Rfq.Web` in this commit.

---

## 1. Commit boundary

This commit is the **semantic refactor**.

It must include all backend changes necessary for the new domain model to compile, persist, and pass tests:

- Domain model/state refactor
- Domain transitions
- Application use-case adaptation
- Infrastructure/persistence adaptation
- minimal API adaptation needed to preserve HTTP contracts
- DB migration required by this semantic change
- tests for the changed invariants and known correctness defects
- canonical design docs update

Do **not** mix in the separate structure/hygiene commit beyond what is necessary to implement this semantic model.

Specifically defer to commit 2:

- solution-wide formatting churn
- broad unrelated folder/file moves
- comments-only changes across untouched code
- XML comment coverage pass
- `.editorconfig` cleanup pass
- enabling/tightening warnings-as-errors if it creates unrelated churn
- purely mechanical one-type-per-file cleanup outside files materially changed here

However, **new or materially rewritten Domain types should be placed directly into the target Domain folders described below**, so they do not need to be moved again immediately.

Every commit must end with a buildable/testable repository. Do not introduce temporary compatibility APIs merely to make an intermediate commit compile.

---

## 2. Architectural rule: Domain data vs Domain transitions vs Application use cases

Use this separation consistently.

### Domain data

Domain data represents valid business state.

- immutable from callers
- typed values rather than raw primitives where a domain concept exists
- constructors/factories enforce construction invariants
- no repository, DB, clock, current-user, calculation-client, or other I/O dependency

### Domain transitions

A transition represents a **business transition category**, not an implementation/data-count category.

Classify a transition by *what business transition it is*, not by how many domain objects it touches.

A transition may consume and return multiple domain values if the business transition requires them.

Examples:

- `QuoteTransitions.Confirm` may validate a `WorkingQuote`, create a `ConfirmedQuote`, and return an updated `RfqCase`.
- `AmendmentTransitions.Confirm` may change the current revision, supersede the old revision, clear presentation/current quote state, and request a revised quote.

Do **not** change classification merely because a future requirement causes an operation to update one more domain object.

Transitions must be deterministic from their inputs. They do not query repositories or allocate current time themselves.

### Application use cases

`Rfq.Application` remains the Use Case layer. Do **not** create a separate `Rfq.UseCases` project.

Application use cases own orchestration:

```text
load/query required state
-> authorization
-> allocate IDs / resolve current time / business date
-> call Domain transition/factory
-> persist all outputs
-> append required events
-> one atomic commit
```

Application use cases may call several domain APIs inside one transaction.

---

## 3. Target Domain state model

Replace the current field-combination source of truth with typed lifecycle state.

### 3.1 RFQ lifecycle hierarchy

The logical hierarchy is:

```text
RfqLifecycle
├─ DraftRfq
├─ OpenRfq
│  ├─ ActiveRfq
│  │  └─ ActiveQuoteState
│  │     ├─ QuoteRequested
│  │     └─ QuoteConfirmed
│  └─ PresentedRfq
├─ CancelledRfq
└─ ClosedRfq
```

Use ordinary C# types/records; do not use preview union syntax.

`OpenRfq` should be an abstract lifecycle subtype with state common to open RFQs.

`PresentedRfq` is a distinct subtype under `OpenRfq`.

### 3.2 Active quote state

`ActiveRfq` must carry exactly one typed active quote state:

```text
QuoteRequested(reason)
QuoteConfirmed(quoteId)
```

This replaces the Domain source-of-truth combination of:

```text
QuoteStatus
QuoteRequestReason?
CurrentQuoteId?
```

The impossible combinations must no longer be publicly constructible.

`PresentedRfq` must require the current `QuoteId`; being Presented therefore implies a valid confirmed quote.

### 3.3 Boundary status enums

`RfqStatus` and `QuoteStatus` may remain for:

- DB projection columns
- API DTOs
- query DTOs
- event/display mapping

but they are **not** the Domain source of truth.

Derive them from lifecycle/state types when mapping outward.

Do not keep a second mutable Domain status value in sync with the typed state.

---

## 4. Case-level responsibility and open ownership

### 4.1 Case-level fields

`ContactOwnerId` and `AssignedTraderId` are Case-level values.

They must not be duplicated inside `ActiveRfq`, `PresentedRfq`, `CancelledRfq`, and `ClosedRfq`.

They survive cancellation/close because they are Case facts/current responsibility, not lifecycle-subtype fields.

### 4.2 Ownership

Replace `bool Owned` in the Domain with a typed ownership state:

```text
Ownership
├─ Unowned
└─ Owned
```

The property should be named `Ownership`, not `Owned`.

Ownership exists only while the RFQ is Open.

- Cancel/Close remove the operational ownership state by leaving `OpenRfq`.
- Reopen starts `Unowned`.
- `Owned` means the current `AssignedTraderId` is the owning trader; do not add a second owner ID.

Persistence may continue using a boolean column if convenient; map it at the Infrastructure boundary.

UI wording may continue to use “Picked Up”; Domain terminology is `Ownership` / `Owned` / `Unowned`.

---

## 5. Immutable Domain data

The following business-state types must no longer expose mutating methods or public setters:

- `RfqCase`
- `RfqRevision`
- `WorkingQuote`
- `CaseMemo`
- lifecycle/state types

`ConfirmedQuote` remains immutable.

Immutability means callers cannot mutate an already-created domain object. A transition returns the next domain value/state.

Do not introduce public `init`/`with` escape hatches that permit invalid state construction.

A `sealed class` with get-only properties is acceptable; everything does not need to become a record.

Remove current mutation-style APIs such as:

```text
rfq.PickUp(...)
rfq.ConfirmQuote(...)
rfq.Present(...)
workingQuote.ApplyCalculated(...)
revision.Confirm(...)
memo.UpdateSales(...)
```

from the public Domain surface.

Low-level helpers may remain `private`/`internal` where required to implement a valid public factory/transition, but Application code must use the public Domain business API.

---

## 6. Transition categories

Create/organize the Domain transition API by **business transition category**.

Required transition groups:

### `RfqLifecycleTransitions`

Own lifecycle/customer-facing transitions such as:

- `ConfirmInitial`
- `Present`
- `Unpresent`
- `Cancel`
- `Reopen`
- `Close`
- `CorrectOutcome`

`Present` belongs here, not to quote editing: its primary meaning is that the quote is in the customer-facing Presented state.

`Close` must also discard a pending amendment Draft as part of the same business transition when required by the canonical rules.

### `RfqOwnershipTransitions`

Own trader ownership/routing transitions:

- `PickUp`
- `Release`
- `Assign`
- `TakeOver`

These may update Case-level `AssignedTraderId` plus Open `Ownership` together.

### `QuoteTransitions`

Own current quote workflow transitions:

- `Confirm`
- `Withdraw`
- `Expire`

`Expire` remains a Quote transition even when it also changes `PresentedRfq` back to `ActiveRfq`: classification is by business meaning, not number of state objects changed.

### `AmendmentTransitions`

Own amendment business transitions:

- `SaveDraft`
- `Confirm`
- `Discard`

Do not expose a generic public `RevisionTransitions.Confirm` that lets Application confirm a revision independently of Case/current-revision rules.

Internal revision helpers are allowed.

### `WorkingQuoteTransitions`

Own WorkingQuote editing transitions:

- `ApplyCalculated`
- `SwitchMode`
- `UpdateManual`

Each returns a new `WorkingQuote`.

### `CaseMemoTransitions`

Own memo updates:

- `UpdateSales`
- `UpdateTrader`

Each returns a new `CaseMemo`.

### Contact Owner change

Keep Contact Owner handoff as an explicit Domain transition; do not put the assignment directly in an Application handler. Use `RfqResponsibilityTransitions.ChangeContactOwner`. Do not conflate Contact Owner responsibility with trader `Ownership`.

### Initial Draft edit

Do not expose low-level mutable revision methods. Initial-Draft edit/discard must also be performed through an explicit Domain transition API. Use `InitialDraftTransitions` for initial-Draft update/discard behavior; do not expose a generic public `RevisionTransitions`.

---

## 7. Quote confirmation must be one coherent Domain operation

Application must not be able to independently create a current `ConfirmedQuote` and separately mark an RFQ quoted through unrelated public calls.

The public Domain API for quote confirmation must preserve the invariant:

```text
ActiveRfq(QuoteRequested)
+ current WorkingQuote
+ new Quote identity / confirmation metadata
        ->
ActiveRfq(QuoteConfirmed(new QuoteId))
+ new immutable ConfirmedQuote
```

A suitable shape is conceptually:

```csharp
QuoteConfirmationResult QuoteTransitions.Confirm(
    RfqCase rfq,
    WorkingQuote workingQuote,
    QuoteId quoteId,
    QuoteConfirmation confirmation);
```

where the result contains at least:

```text
updated RfqCase
new ConfirmedQuote
```

The exact record/class names may differ slightly, but preserve the boundary.

### `QuoteConfirmation`

Treat confirmation metadata as a real domain concept rather than a bag of unrelated primitive arguments.

It contains:

- `ConfirmedBy : UserId`
- `ConfirmedAt : DateTimeOffset`
- typed quote expiry policy

`QuoteId` remains separate because it is the identity of the new `ConfirmedQuote`, not confirmation metadata.

### Quote ID allocation

The Application layer allocates the new `QuoteId` (for example by calling `QuoteId.New()`) and passes it into the Domain transition.

Do not create `IQuoteIdGenerator` for a local Guid unless there is a concrete need.

Do not call `Guid.NewGuid()` inside the transition itself.

Apply the same principle to `RevisionId`: allocate it in Application and pass the value to Domain creation/transition APIs.

`CaseId` remains DB/application-infrastructure allocated according to the existing sequence design.

---

## 8. Quote expiry must be typed

Do not keep `int? expiryMinutes` as the core Domain concept.

Introduce a typed quote-expiry policy that supports the currently implemented cases:

- no expiry
- expire after a positive duration

Keep the type extensible for future business policies such as a fixed time / AM / PM / EOD without designing those future policies now.

The Domain type should validate the duration.

Persistence may continue using the current `expiry_minutes` and resolved `expires_at` representation for the current supported policies.

`ConfirmedQuote` remains an immutable snapshot and stores the resolved expiry information needed by the current worker/history.

---

## 9. WorkingQuote creation

WorkingQuote creation is a Domain factory concern, separate from WorkingQuote mutation transitions.

Provide a domain factory such as `WorkingQuoteFactory`.

Initial Confirm flow should be conceptually:

```text
Application ConfirmInitial use case
  -> RfqLifecycleTransitions.ConfirmInitial(...)
  -> WorkingQuoteFactory.CreateInitialFor(confirmedRfq, ...)
  -> persist both
  -> one commit
```

The factory must only create a WorkingQuote for a valid confirmed/current revision state.

Do not use a generic Infrastructure `EnsureWorkingQuote` operation as the primary business creation API.

The database still enforces at most one WorkingQuote per Revision.

It is acceptable for Application/Infrastructure to defensively detect an already-existing WorkingQuote for idempotency/recovery, but that is persistence/recovery behavior, not the central Domain model.

### Amendment WorkingQuote

When an amendment is confirmed, the new current Revision must receive a new WorkingQuote in the same application transaction. Seed it from `QuoteSeedRevisionId` when specified; otherwise use the canonical previous/current seed behavior.

The seed WorkingQuote is loaded by Application/Infrastructure. Domain code decides how to create/clone the new WorkingQuote from the supplied seed.

Do not reactivate or reuse the old Revision's WorkingQuote as the new Revision object.

---

## 10. RfqCase / Revision cleanup

Fix the current `InitialRevision` semantic bug.

The Case must expose the actual current revision as `CurrentRevision`; do not store a property called `InitialRevision` and later replace it with an amendment revision.

Do not retain temporary mutable tracking fields such as `PreviousRevision` / `DiscardedRevision` merely so Application can inspect what a mutation just did.

If a transition needs to expose affected old/new revisions to persistence/events, return them in a typed transition result.

### Revision terms

Extract a value object for the revision-owned condition set where it improves the model, e.g. conceptually:

```text
RevisionTerms
- Notional
- SettlementDate
- StandardSettlementDate
- SalesAndTradingMessage
```

Draft values may still be incomplete where the workflow allows it; confirmation performs the stronger validation.

Do not create arbitrary wrapper objects solely because a method has many arguments. Create a sub-object when the values form a real domain concept or invariant.

---

## 11. Typed identifiers and Application primitives

Inside Domain and Application business logic, use typed domain values wherever they carry domain meaning:

- `CaseId`
- `RevisionId`
- `QuoteId`
- `ClientId`
- `SecurityId`
- `CategoryId`
- `UserId`
- typed statuses/state
- typed ownership
- typed versions

Remove internal patterns such as:

```text
string QuoteStatus
string AssignedTraderId
string SecurityId
long CaseId
Guid RevisionId
```

from Application context/authorization/result types when those values are domain concepts.

Raw `long` / `Guid` / `string` are acceptable at boundaries such as:

- HTTP request/response DTOs
- EF entities/columns
- JSON serialization
- external calculation/integration transport if required by that protocol
- error messages/codes/search text/memos

Map to/from Domain types at the boundary.

Preserve existing public HTTP JSON shapes unless a change is absolutely necessary.

---

## 12. `StateVersion`

Introduce one common Domain value type:

```text
StateVersion
```

Requirements:

- wraps signed `long`
- value must be >= 1
- provides checked `Next()`
- no `ulong`

Use it for Domain/Application concurrency versions of:

- RFQ current state
- Revision
- WorkingQuote
- CaseMemo

Property name should generally be `Version`; the type already conveys that it is a state version.

Infrastructure/DB/API may continue to store/transmit `long` and map at the boundary.

Do not create separate `CaseVersion`, `RevisionVersion`, etc. unless a concrete bug demonstrates that the extra distinction is needed.

---

## 13. Category remains master data, not an enum

Do **not** convert Category to a compile-time enum.

Use the existing master-data model as the source of valid categories:

```text
Category master
- CategoryId   // stable key
- Name         // display name; may change
```

`Security.CategoryId` and `CategoryRouting.CategoryId` must refer to the category master through FK constraints.

The Domain/Application uses typed `CategoryId`, not arbitrary raw strings.

The reason for separating ID and Name is that changing the display name must not break references/routing/history.

Routing remains data/configuration:

```text
CategoryId -> DefaultTraderId
```

Security-to-category mapping is not assumed to be a fixed code rule; it may depend on DB/master data.

Do not add speculative category-management UI or category enum values in this commit.

---

## 14. Authorization boundary

Keep centralized `IRfqAuthorization` (or equivalent central Application authorization service).

Separation:

### Domain transitions validate state/business possibility

Examples:

- cannot Withdraw a Presented RFQ
- cannot Confirm a quote unless Active + Requested
- cannot Unpresent unless Presented
- cannot Release when ownership state is Unowned

### Application authorization validates who may perform the operation

Examples:

- only Contact Owner may Present/Unpresent/Close
- only owning trader may Release/quote
- role/desk constraints
- future Manager overrides

Do not put repository/current-user lookup inside Domain transitions.

Do not scatter role/actor predicates across controllers/use cases.

---

## 15. Domain construction and persistence rehydration

Normal Domain creation must use public factories / public transition APIs that create valid business state.

Persistence rehydration is a separate concern.

Use:

- non-public constructors
- `internal Restore` / equivalent rehydration APIs
- `InternalsVisibleTo("Rfq.Infrastructure")` as needed
- optionally `InternalsVisibleTo("Rfq.Domain.Tests")`

Do not make arbitrary public `Restore(...)` methods an escape hatch for Application code.

A snapshot/DTO may be introduced only when it is a useful logical grouping of rehydration data; do not add persistence DTOs to Domain merely to reduce parameter count.

Infrastructure maps persistence entities to valid Domain state and should detect impossible persisted combinations as invariant failures.

---

## 16. Domain exception taxonomy

Replace generic `InvalidOperationException` / `ArgumentException` for normal Domain-rule failures with a small Domain exception taxonomy.

Use approximately these categories:

```text
DomainException
├─ DomainRuleViolationException
├─ DomainValidationException
├─ StateVersionMismatchException
└─ DomainInvariantException
```

Meaning:

- `DomainRuleViolationException`: valid data, but requested business transition is not allowed in the current state
- `DomainValidationException`: invalid domain input/value
- `StateVersionMismatchException`: expected version does not match current state version
- `DomainInvariantException`: impossible/corrupt state detected; treat as bug/data-integrity issue rather than normal user error

Do not create one exception subclass per operation (`CannotPresentException`, etc.).

Stable error codes may be carried where useful for API mapping, but do not build a large generic Result/error framework.

Keep non-domain failures separate:

- NotFound
- Forbidden/authorization
- calculation failure
- DB/infrastructure failure
- cancellation

Preserve or improve current API error mappings, especially 409 for concurrency conflict.

---

## 17. Persistence model

The persistence shape does **not** need to mirror the typed Domain hierarchy.

It is acceptable to keep `CaseCurrent` flattened with columns such as:

- lifecycle/status projections
- quote-status projections
- current revision / quote references
- contact owner / assigned trader
- ownership boolean
- version

but these are persistence/read projections and must be derived/mapped from the typed Domain state.

Do not reintroduce the flattened fields as mutable Domain source-of-truth just because they exist in the DB.

Keep optimistic concurrency columns as `bigint`/`long` in PostgreSQL/EF and map to `StateVersion`.

---

## 18. Remove `QuoteEvent.CaseId`

Change logical/persistence `QuoteEvent` to:

```text
QuoteEvent
- EventId
- QuoteId
- Type
- Payload
```

Remove the denormalized `CaseId` column/FK from `quote_events`.

The Case is uniquely derived through:

```text
QuoteEvent.QuoteId
-> ConfirmedQuote.RevisionId
-> RfqRevision.CaseId
```

Queries that need CaseId must join through that relation.

Add the required EF migration and update seed/query/event-writing code.

Do not retain both `CaseId` and `QuoteId` without a DB-enforced cross-key consistency constraint.

---

## 19. Event cursor correctness

Preserve the existing no-loss/commit-order-safe global event cursor behavior.

The current single-row cursor serialization with a DB lock is acceptable if it remains held through event/state save and transaction commit.

Add an infrastructure integration test that explicitly proves the reverse-commit case:

1. two event-producing transactions start
2. ordering is arranged so the transactions attempt event creation in one order
3. they commit in the opposite order / one is delayed while the other attempts to proceed
4. after both complete, `GetEventsAfter` cannot permanently miss either event

The exact test orchestration may reflect the actual lock mechanism, but it must test the canonical no-loss property with real PostgreSQL/Testcontainers.

Do not replace this with a plain identity/sequence cursor.

---

## 20. Fix `CreateFromExisting` business-date comparison

The current implementation compares business `today` with:

```text
DateOnly.FromDateTime(source.CreatedAt.UtcDateTime)
```

This is incorrect near the desk timezone date boundary.

Fix the rule:

```text
source Case created on current desk/business date
    -> copy source actual SettlementDate
otherwise
    -> do not copy the old actual settlement; use current standard settlement flow
```

Resolve the source creation **business date using the configured desk/business timezone/date abstraction**. Do not compare the UTC calendar date to business `today`.

Add a test covering a UTC timestamp whose JST/business date differs from its UTC date.

Do not add holiday/calendar logic beyond the existing business-date definition solely for this fix.

---

## 21. Calculation / WorkingQuote revalidation

Preserve the existing safe calculation flow:

1. load typed edit context and expected versions
2. authorize
3. perform calculation outside DB transaction
4. reload current RFQ state
5. revalidate current revision, ownership/authorization, RFQ version, and WorkingQuote version
6. apply `WorkingQuoteTransitions.ApplyCalculated`
7. persist

Use typed IDs/state in `QuoteEditContext` and authorization state. Remove string comparisons such as `QuoteStatus == "Requested"`.

Calculation failure must still leave WorkingQuote unchanged and append the failure log.

---

## 22. Target folder organization

This is the target structure. New/materially rewritten Domain files in this commit should be placed here. Broad relocation of untouched Application/Infrastructure code is deferred to commit 2.

```text
src/Rfq.Domain/
  Rfqs/
    RfqCase.cs
    CaseMemos/
      CaseMemo.cs
    Revisions/
      RfqRevision.cs
      RevisionTerms.cs
      RevisionStatus.cs
    Lifecycle/
      RfqLifecycle.cs
      DraftRfq.cs
      OpenRfq.cs
      ActiveRfq.cs
      PresentedRfq.cs
      CancelledRfq.cs
      ClosedRfq.cs
      ActiveQuoteState.cs
      QuoteRequested.cs
      QuoteConfirmed.cs
      Ownership.cs
      Owned.cs
      Unowned.cs

  Quotes/
    WorkingQuote.cs
    ConfirmedQuote.cs
    QuoteConfirmation.cs
    QuoteExpiry.cs
    WorkingQuoteFactory.cs
    ...quote payload/value types...

  Transitions/
    RfqLifecycleTransitions.cs
    RfqOwnershipTransitions.cs
    QuoteTransitions.cs
    AmendmentTransitions.cs
    WorkingQuoteTransitions.cs
    CaseMemoTransitions.cs
    InitialDraftTransitions.cs
    RfqResponsibilityTransitions.cs

  Identities/
    CaseId.cs
    RevisionId.cs
    QuoteId.cs
    ClientId.cs
    SecurityId.cs
    UserId.cs
    CategoryId.cs

  Values/
    StateVersion.cs

  Errors/
    DomainException.cs
    DomainRuleViolationException.cs
    DomainValidationException.cs
    StateVersionMismatchException.cs
    DomainInvariantException.cs

  ReferenceData/
    ...only Domain reference-data value types if actually needed...
```

Do not introduce a `Common`, `Shared`, or generic `Primitives` dumping-ground.

For this commit, keeping namespace `Rfq.Domain` across these folders is acceptable and preferred if changing namespaces would add noise. Folder structure does not require namespace churn.

### Application target organization

The eventual Application structure is:

```text
src/Rfq.Application/
  Abstractions/
    Persistence/
    Calculation/
    Events/
    Identity/
    Time/

  Authorization/

  Rfqs/
    Creation/
    Amendments/
    Ownership/
    Lifecycle/

  Quotes/
    WorkingQuotes/
    Confirmation/
    Presentation/

  Queries/
    ActiveRfqs/
    History/
    PastRfqs/
    Eod/

  GridConfig/
```

Do **not** perform a bulk Application file-move-only refactor in this commit. Commit 2 will finish mechanical organization and one-type-per-file cleanup. New files may use the target location when it does not obscure the semantic diff.

---

## 23. API boundary

Keep the API as the outer primitive boundary.

Controllers may accept/return DTO fields such as `long`, `Guid`, and `string` because those are transport representations.

Map them to typed Application/Domain values immediately.

Do not leak Domain state subclasses directly as the public HTTP contract unless the existing API already does so.

Preserve existing endpoint behavior/JSON shapes where possible so the frontend does not need to change in this backend refactor.

No frontend changes in this commit.

---

## 24. Category/persistence migration scope

The repository already has category and routing tables. Reuse them.

Verify and preserve:

- category master keyed by CategoryId
- category display Name separate from ID
- Security -> Category FK
- CategoryRouting -> Category FK
- CategoryRouting -> trader FK

Do not create a duplicate category system.

Only add a DB migration when the semantic changes require an actual schema change. `QuoteEvent.CaseId` removal definitely requires one. Domain-only type changes such as `StateVersion` and `Ownership` do not require changing the existing `bigint`/boolean columns merely for aesthetic symmetry.

Migration must preserve existing data and must not require `reset-dev` to work.

---

## 25. Tests required in this commit

Update/add tests so the refactor is behaviorally reviewable.

### Domain tests

Cover at least:

- lifecycle hierarchy and inability to construct invalid Active/Presented/quote-state combinations through public APIs
- `Present`: Active+confirmed -> Presented
- `Unpresent`: Presented -> Active+confirmed
- `Withdraw`: Active+confirmed -> Active+requested(Withdrawn); Presented rejected
- `Expire`: Active or Presented confirmed -> Active+requested(Expired)
- `Cancel` / `Reopen` and ownership reset to Unowned on reopen
- Close requires current confirmed quote and discards pending amendment
- ownership PickUp / Release / Assign / TakeOver
- Quote Confirm returns a mutually consistent updated RFQ + ConfirmedQuote
- Quote Confirm requires WorkingQuote to belong to current Revision
- WorkingQuote mode/calculated/manual transitions remain equivalent to current business behavior
- amendment Save/Confirm/Discard
- amendment Confirm supersedes old current revision, confirms draft, switches current revision, clears presentation/current confirmed quote, and requests Revised
- CaseMemo transitions
- `StateVersion` validation and `Next()`
- Domain exception categories for representative invalid transitions

### Application tests

Cover at least:

- typed Application contexts/commands still orchestrate the same use cases
- authorization remains centralized and actor checks are not moved into controller code
- quote confirmation allocates QuoteId outside Domain transition
- initial confirm creates WorkingQuote via Domain factory and commits atomically
- amendment confirm creates/seeds the new Revision WorkingQuote in the same transaction
- calculation success revalidates after external calculation; failure does not mutate WorkingQuote
- `CreateFromExisting` business-date boundary regression

### Infrastructure tests

Use real PostgreSQL/Testcontainers for:

- migration succeeds from current schema
- `quote_events.case_id` is removed and event query still resolves required Case context through joins
- one WorkingQuote per Revision constraint remains
- one Draft Revision per Case remains
- concurrency tokens still work with `StateVersion` mapping
- reverse-commit/no-loss event cursor test

### API tests

Update only as necessary to prove existing external contracts still work after internal typed refactor.

---

## 26. Behavior that must not change

Unless explicitly changed above, preserve current canonical business behavior, including:

- one Case -> one final Hit/Away outcome
- amendment Draft does not invalidate current confirmed revision/quote until amendment Confirm
- Presented protects against Withdraw
- Presentation not required for Close
- Withdraw/Expire retain WorkingQuote
- Cancel/Reopen do not resurrect old ConfirmedQuote
- Reopen is Requested/Reopened and Unowned
- Close records `ClosedQuoteId`
- outcome correction is explicit Hit <-> Away
- WorkingQuote calculation failure leaves current WorkingQuote unchanged
- persisted events and SSE wake-up semantics remain
- event cursor remains globally ordered/no-loss
- current confirmed quote history remains immutable
- current HTTP behavior should remain frontend-compatible

---

## 27. Deliberate non-goals for this commit

Do not:

- create another project for UseCases
- add MediatR
- add event sourcing
- add generic repository/result/framework abstractions
- add a generic state-machine framework
- add dynamic category administration UI
- change the frontend
- redesign authentication
- redesign calculation transport
- implement future expiry policies beyond current None/fixed-duration support
- add a separate ID-generator interface for Guid IDs without a concrete need
- perform broad formatting/comments/file-splitting outside materially changed code

---

## 28. Verification commands

Run at minimum from repository root:

```bash
dotnet restore
dotnet build Rfq.sln
dotnet test Rfq.sln
```

Run the PostgreSQL integration suite with Docker/Testcontainers available.

Apply migrations to a database created from the **pre-refactor current migration history**, then verify the new migration applies without destructive reset.

If repository scripts provide equivalent standard commands, use them and report the exact commands.

No frontend build is required unless a backend contract change accidentally forces one; the intended outcome is no frontend change.

---

## 29. Commit requirement

Produce **one commit** for this instruction.

Suggested commit subject:

```text
refactor: make RFQ domain state immutable and typed
```

Do not include the later structure/hygiene pass in this commit.

At completion, report:

1. summary of semantic changes
2. any small implementation choices made where this instruction allowed equivalent naming
3. DB migration summary
4. tests/commands run and results
5. any remaining item intentionally deferred to commit 2

Do not start commit 2 automatically.
