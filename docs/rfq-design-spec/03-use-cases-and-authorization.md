# 03. Use Cases and Authorization

## 1. Application use cases

Initial use-case surface:

### RFQ / Revision

- `CreateDraft`
- `ConfirmInitialRevision`
- `UpdateDraftRevision`
- `ConfirmRevision`
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

This list defines application responsibilities, not mandatory one-to-one HTTP endpoints.

---

## 2. Command / Query separation

Use a lightweight command/query split.

### Commands

Commands change state and pass through domain/application rules and repositories.

Examples:

- ConfirmRevision
- ConfirmQuote
- WithdrawQuote
- CloseHit
- CancelRfq

### Queries

Queries do not mutate business state and may read directly into DTOs optimized for screens.

Examples:

- SearchPastRfqs
- GetEodSummary
- GetRevisionHistory
- GetQuoteHistory

Do not require query paths to reconstruct full domain aggregates.

A useful rule:

```text
Command side = minimum domain state required to make a valid business decision
Query side   = efficient projection shaped for the screen
```

---

## 3. Authorization boundary

Centralize business authorization in one service rather than scattering checks through handlers.

Conceptually:

```text
IRfqAuthorization
- CanEditRevision(...)
- CanConfirmRevision(...)
- CanQuote(...)
- CanPresent(...)
- CanClose(...)
- CanTakeOver(...)
- CanChangeContactOwner(...)
- ...
```

Inputs may include:

- Current User
- roles
- Desk
- Contact Owner
- Assigned Trader
- Owned
- Manager/override permissions in the future

Do not embed repeated checks such as `role == Trader && assignedTrader == me && owned` throughout the application.

---

## 4. Roles and overlap

Roles are not necessarily mutually exclusive.

Conceptually:

```text
Sales
Trader
Manager   // future
```

A user may hold multiple roles.

Manager is not automatically an RFQ owner. Manager-specific overrides should be added in the centralized authorization layer later.

Initial implementation may allow both Sales and Trader views to be accessible while identity/role plumbing is still simplified.

---

## 5. Initial authorization matrix

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

\* If the Contact Owner is also the owning Trader, their Trader role/ownership grants quote access.

Access is still constrained to the relevant Desk/application scope.

---

## 6. Current user

The application assumes a server-side `CurrentUser` abstraction is available.

At minimum:

```text
UserId
Roles
DeskId
```

The mechanism by which the existing environment identifies the user is outside the initial design.

The business/application layers should not depend on a specific Authorization-header scheme.

---

## 7. Optimistic concurrency

Use optimistic concurrency rather than long-lived locks.

Versioned mutable state includes at least:

- `CaseCurrent`
- `RfqRevision`
- `WorkingQuote`

FE sends expected version.

Update pattern:

```text
UPDATE ... WHERE Version = expectedVersion
```

On success:

```text
Version = Version + 1
```

On mismatch:

- return Conflict
- reload the affected RFQ
- discard the conflicting FE edit
- show a concise toast

Do not auto-merge in the initial implementation.

---

## 8. Critical concurrent transitions

Example:

```text
Sales: Confirm Rev2
Trader: Confirm Quote against Rev1
```

Both operations must validate the same current Case/Revision state/version.

Only one can commit against the expected state. The loser gets Conflict and reloads.

Revision Confirm should atomically perform its multi-entity effects.

---

## 9. Error categories

Use a small application-level error taxonomy:

```text
Validation
Conflict
Forbidden
NotFound
CalculationFailure
```

Each error may include a stable `code` and structured details.

Examples:

```text
WORKING_QUOTE_VERSION_MISMATCH
INVALID_SETTLEMENT_DATE
NOT_CONTACT_OWNER
```

Do not create a large exception class hierarchy for every business condition.

HTTP mapping can be conventional, e.g.:

- Validation -> 400
- Forbidden -> 403
- NotFound -> 404
- Conflict -> 409
- CalculationFailure -> 422 or equivalent application response

Bulk operations return per-item success/error rather than failing the entire batch when one item fails.

---

## 10. API contract approach

Use code-first ASP.NET Core endpoints/DTOs as the authoritative implementation contract.

Generate OpenAPI from server implementation.

OpenAPI may later be used to generate FE clients.

Do not spend the initial phase hand-authoring a detailed contract-first OpenAPI document; refine operation names, DTOs, and error schemas once implementation feedback exists.
