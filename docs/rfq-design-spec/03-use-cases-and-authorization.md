# 03. Use Cases and Authorization

## 1. Layer meaning

`Rfq.Application` is the Use Case layer. Do not create a separate `Rfq.UseCases` project.

A useful distinction is:

```text
Domain transition/factory
= given business data and explicit inputs, what valid business state results?

Application use case
= how does the system obtain inputs, authorize the actor, call Domain logic,
  persist outputs, record events, and commit?
```

Application owns:

- repository/query access
- current-user lookup
- role/desk authorization
- Case/Revision/Quote ID allocation orchestration
- current instant / business-date resolution
- external calculation calls
- event recording
- transaction/unit of work

Domain owns:

- construction invariants
- state preconditions
- state transition rules
- coherent creation of related Domain outputs where separate public operations would permit an invalid business state

---

## 2. Application use cases

Representative surface:

### RFQ / Revision

- `CreateDraft`
- `ConfirmInitialRevision`
- `UpdateDraftRevision`
- `ConfirmRevision` / Confirm Amendment
- `DiscardRevision`
- `CreateFromExisting`

### Ownership / responsibility

- `PickUp`
- `Release`
- `TakeOver`
- `AssignTrader`
- `ChangeContactOwner`

### Quote

- `UpdateWorkingQuote`
- `ConfirmQuote`
- `PresentQuote`
- `UnpresentQuote`
- `WithdrawQuote`
- `ExpireQuote`

### Lifecycle

- `CancelRfq`
- `ReopenRfq`
- `CloseHit`
- `CloseAway`
- `CorrectOutcome`

### Memos

- `UpdateSalesMemo`
- `UpdateTraderMemo`

### Query / support

- `SearchRfqs`
- `GetRfqDetail`
- `GetRevisionHistory`
- `GetQuoteHistory`
- `GetEodSummary`
- `GetEventsAfter`
- `GetGridConfig`
- `SaveGridConfig`
- `SearchSecurity`
- `ResolveSecurity`
- `SearchClient`
- `ResolveClient`
- `ResolveRfqDefaults`
- `ResolveStandardSettlement`
- `CalculateBulk`

This is application responsibility, not mandatory one-to-one HTTP endpoint naming.

---

## 3. Use-case orchestration pattern

A mutating use case normally follows:

```text
load/query required state
-> central authorization
-> validate expected StateVersion values
-> allocate IDs / resolve time/business date
-> call Domain transition/factory
-> persist all outputs
-> append required persisted events
-> one atomic commit
```

Do not move repository access into Domain transitions.

Domain transition inputs may use a typed value object where values form a real domain concept (for example `QuoteConfirmation`). Do not create wrapper objects solely because a method has many arguments.

---

## 4. Typed values in Application

Inside Application business logic, use Domain types rather than raw transport primitives whenever a Domain concept exists.

Examples:

```text
CaseId       not long
RevisionId   not Guid
QuoteId      not Guid
SecurityId   not string
UserId       not string
CategoryId   not string
StateVersion not long
```

Do not compare statuses as strings.

Raw primitives remain appropriate at HTTP, EF/DB, JSON, and external transport boundaries.

API controllers map raw DTOs to typed Application inputs immediately and map typed results back outward.

---

## 5. Command / Query separation

Use a lightweight command/query split.

### Commands

Mutate state and pass through authorization + Domain transitions + repositories.

### Queries

Read-only paths may query directly into screen DTOs without reconstructing full Domain objects.

Rule:

```text
Command side = minimum typed Domain state required to make a valid business decision
Query side   = efficient projection shaped for the consumer
```

This is pragmatic separation, not a full CQRS platform.

---

## 6. Authorization boundary

Centralize actor authorization in `IRfqAuthorization` or equivalent Application policy service.

Inputs may include:

- current user
- roles
- Desk
- Contact Owner
- Assigned Trader
- Open Ownership state
- future Manager/override permissions

Do not scatter role/actor predicates across controllers and use cases.

### Domain vs authorization

Domain transitions validate **whether a state transition is valid**:

- Presented cannot Withdraw
- only Active+Requested can Quote Confirm
- Unpresent requires Presented
- Release requires Open+Owned state

Application authorization validates **whether this actor may perform it**:

- Contact Owner may Present/Close
- owning trader may Quote/Release
- role/desk scope
- future Manager override

This distinction must remain even when a rule uses both state and actor data.

---

## 7. Initial authorization matrix

| Operation | Contact Owner | Owner Trader | Assigned Trader, unowned | Other desk member |
|---|---:|---:|---:|---:|
| Revision edit | yes | no | no | no |
| Revision confirm/discard | yes | no | no | no |
| Quote edit/confirm | no* | yes | no | no |
| Pick Up | n/a | n/a | yes | yes, if unowned and accessible |
| Release | no | yes | no | no |
| Take Over | no | n/a | no | yes, for another-owned |
| Present/Unpresent | yes | no | no | no |
| Hit/Away Close | yes | no | no | no |
| Outcome correction | yes | no | no | no |
| Change Contact Owner | yes | no | no | no |
| Sales-only memo | Sales role | no | no | Sales role |
| Trader-only memo | Trader role | yes | Trader role | Trader role |

\* If Contact Owner is also the owning Trader, their Trader role/ownership grants quote access.

Access is still constrained to relevant Desk/application scope.

---

## 8. Current user

The server provides a `CurrentUser` abstraction with at least:

```text
UserId
Roles
DeskId
```

Authentication transport is outside this design.

---

## 9. Optimistic concurrency

Use `StateVersion` in Domain/Application and raw `bigint`/`long` at persistence/transport boundaries.

Versioned state includes at least:

- current RFQ state
- `RfqRevision`
- `WorkingQuote`
- `CaseMemo`

FE sends expected versions.

On mismatch:

- raise/map `StateVersionMismatchException`
- API returns Conflict
- reload affected state
- no automatic merge initially

Database concurrency token behavior remains authoritative for concurrent writes.

---

## 10. Critical concurrent transitions

Example:

```text
Sales: Confirm Rev2
Trader: Confirm Quote against Rev1
```

Both must validate the same current Case/Revision state/version.

Only one may commit against the expected state. The loser gets Conflict/reload.

External calculation flow must re-read/revalidate RFQ/WorkingQuote state after the external calculation returns and before applying the result.

---

## 11. Domain exceptions and application errors

Use a small Domain exception taxonomy:

```text
DomainException
├─ DomainRuleViolationException
├─ DomainValidationException
├─ StateVersionMismatchException
└─ DomainInvariantException
```

Do not create an exception subclass per operation.

Application/API still distinguishes categories such as:

```text
Validation
Conflict
Forbidden
NotFound
CalculationFailure
Invariant/Server fault
```

Stable error codes may be exposed where useful.

Typical HTTP mapping:

- Validation -> 400
- Forbidden -> 403
- NotFound -> 404
- Conflict -> 409
- CalculationFailure -> 422 or application equivalent
- invariant/data-corruption failure -> 5xx/operational alert, not normal user validation

Bulk operations return per-item success/error where already designed.

---

## 12. IDs and time

Application supplies non-deterministic/system values to Domain operations.

- `CaseId`: existing DB/application sequence path
- `RevisionId`: allocate in Application, pass to Domain
- `QuoteId`: allocate in Application, pass to Domain
- `ConfirmedAt` / edit timestamps: obtain from `TimeProvider`, pass to Domain
- business date: obtain from business-date abstraction, pass to Domain

Do not create Guid-generator interfaces without concrete need.

Do not call clock/repository services from Domain transitions.

---

## 13. API contract approach

ASP.NET Core implementation remains code-first and authoritative.

API DTOs may use raw transport primitives.

Preserve existing external JSON/endpoint behavior through internal refactors where practical.

Do not expose the internal lifecycle class hierarchy directly as a transport contract merely because it exists in Domain.

---

## 14. Roles and overlap

Roles are not necessarily mutually exclusive.

Conceptually:

```text
Sales
Trader
Manager   // future policy
```

A user may hold multiple roles.

Manager is not automatically an RFQ owner. Future Manager overrides belong in centralized authorization rather than Domain lifecycle rules or scattered endpoint checks.
