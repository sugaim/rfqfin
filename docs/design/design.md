<!-- BEGIN README.md -->

# JPY Corporate Bond RFQ System — Canonical Design

This directory is the canonical design specification for the initial implementation of the internal JPY corporate bond RFQ system.

The canonical design is intended to be usable by humans, coding agents, and future implementation threads. It records:

- the domain and business rules
- state transitions and invariants
- application boundaries
- UI behavior
- persistence/event design
- runtime/integration assumptions
- test/seed strategy
- explicit non-goals
- the rationale behind non-obvious choices

It deliberately does **not** prescribe a step-by-step implementation sequence. That belongs in a separate implementation instruction/plan.

If an implementation instruction conflicts with this canonical design, **this canonical design wins unless the design is explicitly revised**.

## Document map

1. [01-domain-model.md](01-domain-model.md)  
   Core entities, lifecycle, revisions, quotes, ownership, memos, and invariants.

2. [02-state-transitions.md](02-state-transitions.md)  
   RFQ, quote, revision, expiry, cancel/reopen, close, ownership, and presentation transitions.

3. [03-use-cases-and-authorization.md](03-use-cases-and-authorization.md)  
   Application use cases, authorization rules, command/query split, concurrency, and errors.

4. [04-ui-ux.md](04-ui-ux.md)  
   Sales/Trader/EOD screens, grid behavior, refresh/change tracking, search, pricer, and draft UX.

5. [05-persistence-and-events.md](05-persistence-and-events.md)  
   Logical schema, authoritative vs projection data, event persistence, repository boundaries, transactions, indexing.

6. [06-calculation-and-search.md](06-calculation-and-search.md)  
   Calculation boundary, mock behavior, quote calculation flow, security/client search, and standard settlement.

7. [07-runtime-and-notifications.md](07-runtime-and-notifications.md)  
   ASP.NET runtime layout, SSE wake-up, event retrieval, expiry worker, logging/observability.

8. [08-testing-and-seed.md](08-testing-and-seed.md)  
   Test strategy, PostgreSQL integration tests, mock masters, and demo seed data.

9. [09-scope-and-deferred.md](09-scope-and-deferred.md)  
   Explicit non-goals, deferred features, open design space, and future extensions.

10. [10-design-decisions.md](10-design-decisions.md)  
    Rationale for non-obvious choices that should survive implementation handoff.

A concatenated convenience copy is also provided as [design.md](design.md).

## Design principles

- Keep the RFQ business model independent from persistence details.
- Model large lifecycle changes explicitly; avoid encoding every orthogonal state combination as a separate type.
- Confirmed business facts are immutable snapshots; current operational state is represented separately.
- Use optimistic concurrency and transactional use cases rather than long-lived locks.
- Do not make the browser silently replace actively edited data.
- Keep the first implementation deliberately narrow where business requirements are not yet known.
- Preserve enough provenance and history that later reconciliation and operational investigation remain possible.
- Prefer replaceable boundaries over speculative generalization.

<!-- END README.md -->

---

<!-- BEGIN 01-domain-model.md -->

# 01. Domain Model

## 1. RFQ case identity

An **RFQ Case** represents one customer inquiry that ultimately closes with one Hit/Away outcome.

A useful boundary rule is:

- If a new condition **replaces** a previous condition, it is the same Case with a new Revision.
- If two conditions can coexist and each could independently result in Hit/Away, they are separate Cases.
- Future List / Thread / Portfolio grouping must live outside the Case and must not redefine Case identity.

Case identity fields:

- `CaseId`
- `ClientId`
- `SecurityId`

Changing Client or Security means creating a new Case, not a Revision.

Case-level creation facts also include:

- `CreatedAt`
- `CreatedBy`
- `SalesId?`
- `CategorySnapshot`
- `CopiedFromCaseId?`
- source metadata if/when external sources are added

`ClientId` and `SecurityId` are not amendment-editable.

---

## 2. Lifecycle type

The domain should use a coarse lifecycle type to prevent nonsensical transitions without creating a combinatorial type explosion.

Conceptually:

```fsharp
type RfqLifecycle =
    | Draft of DraftRfq
    | Open of OpenRfq
    | Cancelled of CancelledRfq
    | Closed of ClosedRfq
```

Only the **large lifecycle boundary** is modeled by type. Operational dimensions such as quote-request state, presentation, ownership, and revision generation remain fields inside the relevant lifecycle state.

### Draft

A not-yet-confirmed RFQ. Trader does not see it.

### Open

An RFQ currently in business workflow.

### Cancelled

A temporarily terminal state that can be reopened. Quotes are not resurrected on reopen.

### Closed

A terminal RFQ with a Hit or Away outcome.

---

## 3. UI/business status names

Use the following business-facing status concepts.

### `RfqStatus`

```text
Draft
Active
Presented
Cancelled
Hit
Away
```

Within the domain, `Active` and `Presented` are both forms of `Open`.

`Hit` and `Away` are the closed outcomes; there is no separate user-facing `Closed` status in the initial design.

### `QuoteStatus`

```text
Requested
Quoted
```

Meaning:

- `Requested`: the trader needs to provide/re-provide a quote.
- `Quoted`: there is a currently valid confirmed quote for the current open workflow.

### `QuoteRequestReason`

Meaningful when `QuoteStatus = Requested`:

```text
Initial
Revised
Reopened
Expired
Withdrawn
```

`Requote` is therefore not a status; it is represented as `Requested` with a reason.

For Cancelled and Closed lifecycle states, `QuoteStatus` is no longer an operational state. Historical views may still show the relevant quote via quote references/history.

---

## 4. Open RFQ state

Conceptually:

```fsharp
type OpenRfq = {
    RfqStatus          : OpenRfqStatus      // Active | Presented
    QuoteStatus        : QuoteStatus        // Requested | Quoted
    QuoteRequestReason : QuoteRequestReason option

    CurrentRevisionId  : RevisionId
    ContactOwnerId     : UserId
    AssignedTraderId   : UserId
    Owned              : bool
}
```

Invariant:

- `QuoteRequestReason` is required when `QuoteStatus = Requested`.
- `QuoteRequestReason` is absent when `QuoteStatus = Quoted`.
- `Presented` requires a quoted, current confirmed quote.
- `Owned = true` means the owner is the current `AssignedTraderId`; no separate owner ID is needed.

---

## 5. Revision model

Revision-owned fields:

- `Notional`
- `SettlementDate`
- `StandardSettlementDate`
- `SalesAndTradingMessage`

Revision status:

```text
Draft
Confirmed
Superseded
Discarded
```

Rules:

- A Case has at most one Draft Revision.
- An initial RFQ Draft and an amendment Draft use the same `Draft` status.
- If the Case lifecycle is Draft, the Draft Revision is the initial draft.
- If the Case lifecycle is Open, an additional Draft Revision is an amendment.
- Confirming an amendment:
  - old current Confirmed Revision becomes `Superseded`
  - Draft becomes `Confirmed`
  - `CurrentRevisionId` changes
- Discarding a Draft makes it `Discarded`.
- A discarded initial draft is hidden from normal lists; physical deletion is unnecessary.
- A discarded amendment remains history; restore semantics are not required initially.

Revision provenance may include:

- `CopiedFromRevisionId?`
- `QuoteSeedRevisionId?`

`QuoteSeedRevisionId` says which historical Revision's WorkingQuote should seed the new Revision. It does not mean the new Revision reuses or reactivates the old Revision.

---

## 6. WorkingQuote

A `WorkingQuote` belongs to a Revision.

Logical cardinality:

```text
Revision -> 0..1 WorkingQuote
```

For a **confirmed Revision**, the intended invariant is that a WorkingQuote exists.

Use a common `EnsureWorkingQuote(revisionId)` behavior:

- if it exists: return it
- if absent and a quote seed revision exists: clone that seed WorkingQuote
- if absent with no seed: create an empty/default WorkingQuote

Use this at Revision Confirm and defensively on trader access so an operational data defect is recoverable.

Properties:

- mutable
- versioned for optimistic concurrency
- last successful calculated state once populated
- retained across Withdraw, Expire, Cancel, and Reopen
- historical WorkingQuotes remain attached to their historical Revisions

A WorkingQuote may contain separate payloads for:

- Calculated mode
- Manual mode

and an active mode.

When switching Calculated -> Manual, Manual values start empty. Calculated values are retained separately. Switching back restores the previous Calculated state.

---

## 7. ConfirmedQuote

A `ConfirmedQuote` is an immutable snapshot created when the trader confirms a WorkingQuote.

Logical cardinality:

```text
Revision -> 0..N ConfirmedQuote
```

It contains, as applicable:

- `QuoteId`
- `RevisionId`
- `ConfirmedBy`
- `ConfirmedAt`
- quote mode (`Calculated` or `Manual`)
- driver/input values
- simple-yield slide for close-based calculated quotes
- output values such as price, yields, spreads, accrued, settlement amount, A/L, delta
- calculation context / market context
- expiry policy
- resolved `ExpiresAt?`
- request reason being answered, if useful for audit/search

ConfirmedQuote does **not** change when it is later Presented, Withdrawn, or Expired.

---

## 8. Calculated and Manual quote modes

### Calculated

The edited quote cell determines the driver. Examples include:

- Price
- BBG-like Yield
- Simple Yield
- Internal compound yield
- Internal yield
- BBG-like G-Spread
- BBG-like Compound Spread
- Internal YSC
- BBG-like ASW
- Internal ASW
- BBG-like I-Spread
- Internal I-Spread
- Z-Spread

The exact metric set can evolve, but the design assumes a typed calculation request rather than a generic untyped number.

`SimpleYieldSlide` is independent of the driver.

Initial business logic is close-based:

- previous-business-day close reference state is used as the baseline
- calculation produces a base simple yield
- manual current-day simple-yield slide is applied
- final simple yield = base simple yield + slide

Do not generalize the slide into an arbitrary spread adjustment.

### Manual

Manual mode requires:

- Price
- Final Simple Yield

The two values are independently entered and are not required to be internally consistent.

A Manual ConfirmedQuote stores the manual values and quote identity/lifecycle metadata. It does not mix in calculated spread/context output.

---

## 9. Contact Owner

`ContactOwnerId` means the person currently responsible for customer contact and Hit/Away closure.

It may be a Sales person or Trader.

Initial values:

- Sales-created RFQ:
  - `SalesId = creator`
  - `ContactOwnerId = creator`
- Trader-created RFQ:
  - `SalesId = null`
  - `ContactOwnerId = creator`

The current Contact Owner may hand off to another Sales/Trader.

Contact Owner is Case-level, not Revision-level.

Primary responsibilities:

- edit/confirm/discard Revision Drafts
- Present / Unpresent
- Hit / Away Close
- Outcome correction
- Contact Owner handoff

---

## 10. Assigned Trader and ownership

Routing:

```text
Security -> Category -> Default Assigned Trader
```

- Security -> Category may be release/master dependent.
- Category -> Default Trader is an editable routing configuration.
- Category is snapshotted on Case creation.
- Existing Cases do not auto-reroute when routing/master changes.
- Sales may override the initial assigned trader before initial confirm.

Ownership model:

```text
AssignedTraderId
Owned : bool
```

If `Owned = true`, owner = `AssignedTraderId`.

Operations:

- Pick Up
- Release
- Assign to...
- Take Over

These operations do not directly change RFQ/Quote status.

---

## 11. Memos and messages

### Sales & Trading Message

- Revision-owned
- visible to both Sales and Trader
- changing it on a confirmed RFQ is an amendment

### Sales-only Memo

- Case-owned
- visible/editable to Sales
- independent of Revision
- editable after Close

### Trader-only Memo

- Case-owned
- visible/editable to Trader
- independent of Revision
- editable after Close

Memo updates are not RFQ lifecycle transitions.

---

## 12. Create New from Existing

Always creates a **new Case**.

Copy:

- Client
- Security
- Notional
- Sales & Trading Message

Do not copy:

- Sales-only Memo
- Trader-only Memo
- quote lifecycle/state

Initialize ownership/routing as a new Case:

- Contact Owner = creator
- Sales = creator if creator is Sales, otherwise null
- Assigned Trader = rerun routing

Settlement rule:

- source Case created today -> copy its actual settlement
- older source Case -> use current standard settlement

Store `CopiedFromCaseId?` as provenance only.

<!-- END 01-domain-model.md -->

---

<!-- BEGIN 02-state-transitions.md -->

# 02. State Transitions

## 1. Initial creation

### Unsaved New

Pressing `New` creates only FE-local state.

No Case ID is created until Save Draft or Confirm.

Closing an unsaved New form discards it without persistence.

### Save Draft

Minimum required:

- resolved Client
- resolved Security

Effects:

- create Case
- create initial Revision with `Draft`
- `RfqStatus = Draft`
- Case ID is assigned
- subsequent Draft edits autosave

### Initial Confirm

Validation includes:

- Client resolved
- Security resolved
- Notional > 0
- valid Settlement Date
- Settlement Date >= business `today`
- Contact Owner set
- Assigned Trader set

`today` here is the desk/business date resolved from the configured business timezone/calendar context; it is not the UTC calendar date obtained from the server clock.

Effects:

```text
Lifecycle: Draft -> Open
RfqStatus: Active
QuoteStatus: Requested
QuoteRequestReason: Initial
Revision: Draft -> Confirmed
EnsureWorkingQuote(currentRevision)
```

---

## 2. Quote Confirm

Preconditions include:

- lifecycle is Open
- trader owns the RFQ
- WorkingQuote belongs to current Revision
- expected WorkingQuote version matches
- quote inputs are valid
- required calculation context is present
- no currently valid confirmed quote is already active

Effects:

```text
QuoteStatus: Requested -> Quoted
QuoteRequestReason: cleared
RfqStatus remains Active
ConfirmedQuote snapshot is created
QuoteEvent: Confirmed
WorkingQuote remains
Expiry is resolved
Presented = false / RfqStatus remains Active
```

Quote Confirm does not recalculate. It snapshots the already-successful WorkingQuote.

---

## 3. Present / Unpresent

Only the current Contact Owner may perform these operations.

### Present

```text
RfqStatus: Active -> Presented
QuoteStatus: Quoted -> Quoted
```

Presentation means the Contact Owner explicitly protects the current quote from trader withdrawal while customer-facing exposure is live.

It is not intended as proof that the customer literally saw the quote.

### Unpresent

```text
RfqStatus: Presented -> Active
QuoteStatus remains Quoted
```

After Unpresent, the trader may Withdraw.

Presentation is not required for Hit/Away Close.

---

## 4. Amendment Draft

Editing Revision-owned fields on a confirmed/open RFQ creates or updates the one Draft Revision.

While Draft is being edited:

- current confirmed Revision remains authoritative
- current quote remains valid
- RFQ/Quote statuses do not change
- Sales sees changed-cell highlighting
- Trader does not see Draft contents

Trader may be shown that a draft/update exists only where useful, but amendment is not a separate RFQ lifecycle status.

### Amendment Confirm

Effects:

```text
old Confirmed Revision -> Superseded
Draft Revision -> Confirmed
CurrentRevisionId -> new Revision

RfqStatus -> Active
QuoteStatus -> Requested
QuoteRequestReason -> Revised

current confirmed quote association is cleared
Presented, if any, is cleared

EnsureWorkingQuote(newRevision)
```

WorkingQuote seed:

- normal amendment -> previous relevant Revision WorkingQuote
- return to older conditions -> explicitly selected historical Revision WorkingQuote

### Amendment Discard

Draft -> Discarded.

Current Revision, quote, and statuses remain unchanged.

---

## 5. Withdraw

Trader operation.

Allowed only for a currently quoted, non-Presented RFQ.

Multiple selected RFQs may be withdrawn together.

Rows that are Presented are skipped.

Effects:

```text
RfqStatus -> Active
QuoteStatus -> Requested
QuoteRequestReason -> Withdrawn
current confirmed quote association cleared
WorkingQuote retained
QuoteEvent: Withdrawn
```

A new quote is produced through the normal Quote Confirm flow.

---

## 6. Expiry

Expiry policy in the initial design:

```text
None
N minutes
```

Each Trader has a default expiry setting.

Before Quote Confirm, the expiry column may be overridden for that RFQ.

At Confirm:

- None -> `ExpiresAt = null`
- N minutes -> `ExpiresAt = ConfirmedAt + N minutes`

Expiry is a real state transition executed by the server's background worker.

Effects:

```text
if Presented:
    RfqStatus: Presented -> Active

QuoteStatus: Quoted -> Requested
QuoteRequestReason: Expired
current confirmed quote association cleared
WorkingQuote retained
QuoteEvent: Expired
```

Expiry is not modeled as an `Unpresented` + `Withdrawn` pair of business events. It is its own `Expired` event even though it shares internal mechanics.

---

## 7. Cancel / Reopen

### Cancel

Case-level operation.

Allowed from Open.

Effects:

- Lifecycle -> Cancelled
- current confirmed quote is no longer operationally active
- `Owned = false`
- Assigned Trader retained
- Contact Owner retained
- WorkingQuote retained
- existing amendment Draft may remain Draft
- ConfirmedQuote history remains

### Reopen

Does not resurrect an old quote.

Effects:

```text
Lifecycle: Cancelled -> Open
RfqStatus: Active
QuoteStatus: Requested
QuoteRequestReason: Reopened
Owned = false
```

Assigned Trader and Contact Owner remain.

WorkingQuote values are retained/recovered so the Trader can review and reconfirm without re-entering all values.

Presentation is not restored.

---

## 8. Close: Hit / Away

Only the current Contact Owner may normally Close.

Precondition:

- current RFQ has a valid current quote (`QuoteStatus = Quoted`)
- Presentation is not required

Effects:

```text
Lifecycle: Open -> Closed
RfqStatus: Hit or Away
ClosedQuoteId = quote being closed against
Owned = false
```

Also:

- any pending Draft Revision is automatically Discarded
- WorkingQuote is retained read-only/history
- ConfirmedQuote remains immutable
- Assigned Trader retained
- Contact Owner retained

### Bulk Close

Bulk Hit/Away only closes RFQs that are not already closed.

If a selected Case is already Hit or Away, bulk Close skips it and does not change its outcome.

### Outcome correction

Outcome correction is a separate explicit operation:

```text
Hit <-> Away
```

The Case stays Closed.

Append `OutcomeCorrected` history.

Reason is optional.

---

## 9. Ownership transitions

### Pick Up

Target: unowned RFQ.

- if assigned to self -> `Owned = true`
- if assigned to another trader but unowned -> confirmation, then `AssignedTrader = self`, `Owned = true`
- other-owned RFQs are not eligible; use Take Over

Multiple selection supported.

Shortcut-based bulk Pick Up should always confirm.

### Release

Target: self-owned RFQ.

```text
Owned = false
AssignedTrader unchanged
```

WorkingQuote unaffected.

### Assign to...

Target: unowned RFQ.

```text
AssignedTrader = target
Owned = false
```

Selecting the target trader is sufficient confirmation.

### Take Over

Target: RFQ owned by another trader.

After strong confirmation:

```text
AssignedTrader = self
Owned = true
```

Record history and notify the previous owner.

---

## 10. Contact Owner handoff

Only current Contact Owner may hand off initially.

After confirmation:

```text
ContactOwnerId = target
```

No RFQ/Quote status changes.

Append `ContactOwnerChanged`.

---

## 11. Revision return-to-old-condition

Never reactivate an old Revision.

Instead:

1. create a new Draft Revision
2. copy selected historical conditions
3. set provenance such as `CopiedFromRevisionId`
4. set `QuoteSeedRevisionId` to the relevant old Revision
5. Confirm normally
6. create/seed a new WorkingQuote
7. Trader reconfirms a new quote

This preserves chronological history and avoids resurrecting old state.

<!-- END 02-state-transitions.md -->

---

<!-- BEGIN 03-use-cases-and-authorization.md -->

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

<!-- END 03-use-cases-and-authorization.md -->

---

<!-- BEGIN 04-ui-ux.md -->

# 04. UI / UX

## 1. General principles

- Sales and Trader screens do not need identical layouts.
- Grid editing is a first-class workflow.
- Do not silently refresh active editing state when server changes arrive.
- Separate "there are updates" from "apply those updates".
- User-specific grid layout is persisted server-side.
- Keep unknown future workflows (List/Thread/Bulk) out of the initial UI.

---

## 2. Sales screen

Initial layout:

```text
+-------------------+--------------------------------------+
| Work pane         | Active RFQ Grid                      |
| (always visible)  |                                      |
|                   +--------------------------------------+
|                   | Selected RFQ Revision / history      |
+-------------------+--------------------------------------+
```

The left work pane is a general work area, not only a New form.

Suggested contextual behavior:

- no selection -> New RFQ form
- one selected -> selected RFQ detail/edit
- multiple selected -> bulk actions where supported

The initial version does not need `New Bulk`.

### New RFQ form fields

- Client
- Security
- Notional
- Settlement Date
- Sales & Trading Message
- Sales-only Memo
- Contact Owner
- Assigned Trader

Side is fixed `Customer Sell`.

Security type is currently Bond and need not be editable.

---

## 3. New RFQ form behavior

New begins as FE-local unsaved state.

### Save Draft

Allowed once Client and Security are resolved.

After Save Draft:

- Case ID exists
- initial Draft Revision is persisted
- subsequent field commits autosave

### Confirm

May occur directly without a prior Save Draft if full validation succeeds.

### Standard settlement

Security selection triggers resolution of:

- Category
- default Assigned Trader
- Standard Settlement Date

Display one Settlement field, prefilled with standard.

User may overwrite actual settlement.

Persist both:

- `SettlementDate`
- `StandardSettlementDate`

Highlight if they differ.

Do not persist a redundant `IsOverride` flag.

---

## 4. Revision editing in the grid

Revision-owned cells:

- Notional
- Settlement
- Sales & Trading Message

Editing a confirmed RFQ:

- creates/updates the one Draft Revision
- autosaves on cell commit
- highlights changed cells on Sales side
- does not change the current open/quote status until Confirm

Actions:

- Confirm Draft
- Discard Draft
- Confirm Selected
- Discard Selected

Bulk Confirm/Discard is Case-by-Case. A conflict on one selected RFQ does not have to fail all others.

Trader does not see Draft contents before Sales confirms the Revision.

---

## 5. Trader screen

Initial layout:

```text
+----------------------------------------------------------+
| Active RFQ Grid                             [Open Pricer] |
|                                      Calc Status column   |
+----------------------------------------------------------+
| [Past RFQ] [Changes]                                     |
|                                                          |
| Past RFQ search/results or change history                |
+----------------------------------------------------------+

                                      [right-side Pricer drawer]
```

Trader work is primarily inline in the main grid.

RFQ detail/memo/history may use a temporary modal where necessary.

---

## 6. Quote editing

Quote cells may include:

- Price
- Yield
- Spread family columns
- Slide
- derived outputs

The edited cell determines the calculation driver.

Edit commit:

1. build calculation request
2. calculate
3. on Success:
   - update WorkingQuote
   - update returned result fields
4. on CalculationFailure:
   - do not update WorkingQuote
   - revert attempted UI value
   - show toast
   - persist failure log

`QuoteStatus = Quoted` is normally locked for quote editing.

When Requested again after Revised/Reopened/Expired/Withdrawn, retained WorkingQuote values become editable again.

---

## 7. Manual quote UI

Calculated / Manual mode switch exists in the quote editing area.

Manual fields:

- Price
- Final Simple Yield

On switching into Manual mode, Manual fields start empty.

Calculated state remains retained independently.

Manual confirmation requires both fields.

---

## 8. Presentation / Close / Withdraw

Presentation:

- Contact Owner action
- Presented RFQ cannot be withdrawn by Trader
- Contact Owner may Unpresent, after which Trader may Withdraw

Close:

- Contact Owner selects Hit or Away and closes
- Presentation is not required

Bulk Withdraw:

- multi-select
- withdraw eligible quoted, non-Presented cases
- Presented cases are skipped and reported

Bulk Close:

- closes only still-open cases
- already Hit/Away cases are skipped
- bulk close never silently changes an existing outcome

Outcome correction is a separate explicit action.

---

## 9. Pick Up / Release / Assign / Take Over UX

### Pick Up

- self-assigned + unowned -> may proceed without confirmation
- other-assigned + unowned -> confirmation
- multi-select -> one summary confirmation
- other-owned -> excluded

Shortcut-based bulk Pick Up should always confirm.

### Release

Self-owned only.

No confirmation required initially.

### Assign to...

Unowned only.

Trader selection is the confirmation.

### Take Over

Other-owned only.

Always use strong confirmation.

Show current owner and target self.

Notify previous owner.

---

## 10. Refresh and server changes

Do not auto-refresh grid data when server changes arrive.

Instead:

- show `Updates available`
- user presses Refresh
- Refresh re-fetches authoritative server state

Refresh discards FE-only uncommitted edits.

Autosaved Draft/WorkingQuote data already persisted on the server remains.

---

## 11. Changes tab: two generations

Changes must not disappear immediately after Refresh.

Maintain two event windows in FE:

### Last Refresh

Events that were applied by the most recent Refresh.

### Pending Updates

Events that arrived after the most recent Refresh and have not yet been applied.

Conceptually:

```text
PreviousRefreshEventId
LastRefreshEventId
LatestSeenEventId
```

Windows:

```text
Last Refresh:
(PreviousRefreshEventId, LastRefreshEventId]

Pending:
(LastRefreshEventId, LatestSeenEventId]
```

On Refresh:

```text
Pending -> Last Refresh
Pending becomes empty
```

This is FE-local operational history, not the audit source of truth.

---

## 12. Toasts

Normal changes:

- mark Updates Available only

Important state changes:

- show in-app toast

Initial important event candidates:

- Revision Confirmed
- Cancelled
- Reopened
- Closed
- Take Over
- Contact Owner Changed
- Withdrawn
- Expired

Presented/Unpresented does not need a toast initially.

Server filters important events to those relevant to the current user/screen context.

---

## 13. Past RFQ search

Past RFQ is always available on Trader screen.

Search result unit:

```text
1 Case = 1 row
```

Do not expand Revision history in the normal search result initially.

Filters:

- Date range
- Client
- Security
- Category
- Contact Owner
- Sales
- Assigned Trader
- Outcome / RfqStatus
- Quote state as useful
- Case ID

Typical result columns:

- Date
- Case ID
- Client
- Category
- Security
- Notional
- Contact Owner
- Trader
- RfqStatus
- Price
- Spread Type
- Spread
- Slide
- Yield

Spread Type/Spread may show the quote driver spread if the driver was a spread.

Initial implementation:

- server query
- maximum approximately 20,000 results returned in one batch
- FE ag-Grid performs client-side sort/filter
- if result exceeds the cap, ask user to narrow criteria
- paging/infinite scroll deferred until actual need is observed

---

## 14. EOD screen

EOD is a separate tab because it belongs to a different operating phase of the day.

Assume Contact Owner is always assigned.

Primary view:

| Contact Owner | Open | Hit | Away |
|---|---:|---:|---:|

Click Open to drill into that owner's unclosed RFQs.

EOD is primarily **desk-wide remaining-work management**, not permission for everyone to decide everyone else's outcomes.

Normal Hit/Away authority remains with Contact Owner.

Do not auto-Away open RFQs at EOD.

---

## 15. Pricer

Pricer is an independent scratch tool.

It may open:

- from a selected RFQ, prefilled from that RFQ
- with no RFQ selected, as an empty scratch pricer

Suggested UI: right-side drawer on Trader screen.

Pricer state is independent after load and does not automatically track RFQ changes.

Minimum fields:

- Security
- Notional
- Settlement Date
- Calculation Type
- Calculation Parameter
- Slide
- Result

Apply-back to official WorkingQuote may be added later.

Initial backend is mock.

---

## 16. Grid configuration

Persist grid layouts in DB.

Logical model:

```text
UserGridConfig
- UserId
- ScreenId
- ConfigKey
- Version
- ConfigJson
- UpdatedAt
```

Unique key:

```text
(UserId, ScreenId, ConfigKey)
```

Persist as appropriate:

- column visibility
- order
- width
- pin
- optionally sort/filter state

Application-owned migration must handle release-time column ID rename/drop/add.

Do not rely only on browser localStorage.

<!-- END 04-ui-ux.md -->

---

<!-- BEGIN 05-persistence-and-events.md -->

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

<!-- END 05-persistence-and-events.md -->

---

<!-- BEGIN 06-calculation-and-search.md -->

# 06. Calculation and Search

## 1. Calculation service boundary

The RFQ application should not own security convention, curve resolution, or detailed pricing logic.

The calculation service boundary receives, per item:

- Security ID
- Settlement Date
- Calculation Type
- typed Calculation Parameters

and returns a result per request.

Conceptually:

```text
CalculateBulk(requests[]) -> results[]
```

Each request has a correlation/request ID.

Do not rely on array ordering alone.

---

## 2. Bulk result semantics

One failed calculation does not fail the entire batch.

Example:

```text
Request A -> Success
Request B -> Error
Request C -> Success
```

Result is therefore logically:

```text
CalculationResult =
    Success { ...outputs... }
  | Error { code, message }
```

This supports multi-row trader calculation and bulk workflows.

---

## 3. Initial implementation: mock calculation client

Use an interface such as:

```text
ICalculationClient
```

with:

```text
MockCalculationClient
RealCalculationClient   // future
```

Initial mock requirements:

- deterministic
- arbitrary Security IDs work
- same broad logic for all securities is acceptable
- returns plausible-looking values
- supports typed calculation requests
- can intentionally return per-item failures for testing
- preserves the real bulk request/response shape

The initial goal is UI/application-flow validation, not pricing accuracy.

---

## 4. WorkingQuote update flow

Trader edits a quote cell.

1. determine driver from edited column
2. read current WorkingQuote and expected version
3. build calculation request
4. call bulk calculation interface
5. on Success:
   - commit WorkingQuote/result
   - increment WorkingQuote version
6. on Error:
   - do not update WorkingQuote
   - FE reverts attempted cell
   - show failure toast
   - persist CalculationFailureLog

Prefer calculating before a short DB write transaction.

Do not hold DB locks during an external/heavy calculation.

Use optimistic version check when writing result.

---

## 5. Calculation failure log

Initial design logs failures, not every successful calculation attempt.

Suggested data:

- FailureLogId
- CaseId
- RevisionId
- TraderId
- attempted driver/type/value
- prior WorkingQuote
- calculation/market context
- request payload or reproducible request snapshot/reference
- error code
- error message
- timestamp

The API returns the failure log ID to the UI where useful.

A future requirement for complete calculation auditing can generalize this into `CalculationAttempt`.

---

## 6. Calculation context

Calculated ConfirmedQuotes must preserve enough context for historical reproducibility.

Examples:

- market date / as-of
- snapshot tag
- reference security IDs
- relevant reference yields
- curve/context identifiers
- method-specific inputs

Avoid a giant nullable column forest in the domain model.

Use typed family-specific context payloads.

---

## 7. Standard settlement

Standard settlement is resolved by the calculation library/service, not reimplemented in the RFQ application.

Conceptually:

```text
ResolveStandardSettlementDate(
    SecurityId,
    TradeDate
) -> SettlementDate
```

On security change before confirm, recompute the standard settlement.

Store the resolved standard alongside the actual settlement in the Revision.

Initial mock returns a deterministic plausible date.

---

## 8. Security search API

Security search belongs to the RFQ App Server API boundary, not directly to the calculation server.

Conceptual internal abstraction:

```text
ISecuritySearch
- Search(query)
- Resolve(id)
```

Initial implementation may query local/mock master data.

Future implementation may delegate to another service without changing the FE contract.

Save canonical `SecurityId` on the RFQ.

---

## 9. Security search behavior

One input field supports several search strategies.

Search strategies may run together; do not force every query through one exclusive parser branch.

Union results, deduplicate, then rank.

Exact/normalized exact/structured matches rank above prefix/partial matches.

### Internal code

Short form:

```text
{int}-{int}
```

Normalize conceptually to:

```text
0-02-XXXX-YYYYY
```

Trimming components and zero-padding the latter fields.

Full form:

```text
{int}-{int}-{int}-{int}
```

Normalize widths approximately:

```text
[1, 2, 4, 5]
```

Prefix/partial search is supported.

### BBG-like search

Concept:

```text
Ticker Cpn [Mat] [#Series]
```

Rules:

- trim
- uppercase ticker
- collapse arbitrary whitespace
- coupon is decimal form
- maturity accepts `MM/DD/YY` and `MM/DD/YYYY`
- `#Series` optional
- ticker + coupon may search even without maturity

### ISIN

After trim + uppercase, support prefix candidates.

Useful search candidate:

```text
^[A-Z]{2}[A-Z0-9]{5,10}$
```

- 7–11 characters -> prefix search
- 12 characters -> validate structure/checksum if desired

Do not hardcode JP-only numeric assumptions; ISIN NSIN content can be alphanumeric.

### Result display

Useful candidate columns:

- Japanese security name
- BBG-style display
- Internal Code
- ISIN

Issuer is optional if reliable issuer master data exists.

Limit results to top N and ask for more input when matches are broad.

---

## 10. Client search

Simpler autocomplete/partial search.

Support as available:

- Japanese/English name
- internal client code

Return canonical `ClientId`.

No need for complex parser logic.

---

## 11. Search caching

Initial implementation:

```text
Search -> PostgreSQL
Exact ID lookup -> PostgreSQL
```

Do not preload the entire security master into application memory.

Do not aggressively cache autocomplete query strings.

If performance later requires caching, the first good targets are exact lookups:

```text
SecurityId -> Security
ClientId -> Client
```

rather than arbitrary prefix-query result sets.

---

## 12. RFQ defaults resolver

The FE benefits from a consolidated query after Security selection.

Conceptually:

```text
ResolveRfqDefaults(SecurityId, TradeDate)
```

May return:

- Category
- Default Assigned Trader
- Standard Settlement Date

This keeps the FE simple while allowing the application server to coordinate routing/master/calculation-service lookups.

<!-- END 06-calculation-and-search.md -->

---

<!-- BEGIN 07-runtime-and-notifications.md -->

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
  - SSE wake-up endpoint
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

---

## 2. OpenAPI

Server implementation is authoritative.

Generate OpenAPI from ASP.NET Core endpoint/DTO definitions.

The generated contract may later drive FE client generation.

---

## 3. Server change notification model

Do not stream full authoritative UI state over SSE.

Use SSE as a **wake-up signal**.

Flow:

```text
business transaction
-> Event rows committed
-> SSE sends "changed"
-> FE receives wake-up
-> FE calls GET events after last EventId
-> FE updates Pending Updates / shows important toasts
```

The persisted Event feed is the recovery/source-of-truth mechanism for change retrieval.

SSE is only transport signaling.

---

## 4. SSE behavior

SSE is preferred for RFQ responsiveness, but the domain/application design must not depend on SSE.

Requirements:

- reconnect safely
- no silent data loss
- FE retains last fetched EventId
- after reconnect, fetch persisted Events after that ID

Because the initial design uses one server instance, no cross-instance fan-out mechanism is required.

If SSE becomes operationally problematic behind proxy/LB infrastructure, a short polling fallback can reuse the same `GetEventsAfter` API without changing the event model.

---

## 5. Event retrieval

Because persistence has a shared parent `Event` table, FE needs one cursor:

```text
lastSeenEventId
```

The cursor semantics are based on **committed event visibility**, not merely sequence allocation order. Infrastructure must guarantee that advancing the cursor cannot hide a lower EventId that commits later.

Query concept:

```text
GetEventsAfter(lastSeenEventId)
```

Server resolves child RfqEvent/QuoteEvent information and filters as appropriate for the current screen/user.

This endpoint powers:

- Updates Available
- Pending Updates list
- important toasts
- reconnect catch-up

---

## 6. Expiry BackgroundService

Run an ASP.NET Core `BackgroundService` in the same App Server process.

Initial interval:

```text
10 seconds
```

Worker finds current quoted items with:

```text
ExpiresAt <= now
```

and runs the normal `ExpireQuote` application use case.

Do not implement expiry as an ad-hoc SQL state mutation disconnected from domain/application rules.

---

## 7. Expiry idempotency and concurrency

Initial deployment assumes one App Server, but ExpireQuote should still be safe if attempted more than once.

Use state/version preconditions so only a still-current quoted item can transition.

If a human action already Withdrawn/Closed/Revised the RFQ, expiry should do nothing or return a harmless no-op/conflict outcome.

Future multi-instance deployment may require:

- DB locking strategy
- `FOR UPDATE SKIP LOCKED`
- advisory lock
- distributed coordination/pub-sub

but none is required initially.

---

## 8. Logging, audit, observability

Keep four concerns separate.

### Domain audit

Use:

- RfqEvent
- QuoteEvent

### Business calculation failure

Use:

- CalculationFailureLog

### Technical logging

Use .NET standard:

```text
ILogger<T>
```

Do not invent another generic logging abstraction without a concrete need.

### Tracing / metrics

Use .NET standard primitives:

```text
ActivitySource
Meter
```

This allows later OpenTelemetry exporters without changing application/domain code.

---

## 9. Event retention

Initial implementation does not delete Events by count or age.

Retain events.

A few million rows are not in themselves a reason to introduce retention complexity.

Archive/partition/retention may be added if actual growth or compliance policy demands it.

---

## 10. Important event filtering

Do not toast every Desk event.

The server uses current-user/screen scope to decide which events are important to this user.

Normal events can still set `Updates Available`.

Important-event candidates are documented in the UI spec.

---

## 11. Authentication scope

The environment can identify current user and role(s) somehow.

The exact token/header/session mechanism is not part of this initial design.

The application consumes a `CurrentUser` abstraction.

SSE/auth transport should be adapted to the actual hosting environment rather than driving the domain design.

<!-- END 07-runtime-and-notifications.md -->

---

<!-- BEGIN 08-testing-and-seed.md -->

# 08. Testing and Seed Data

## 1. Test strategy

Use different test styles for different risks.

### Domain unit tests

Focus on state-machine/business invariants:

- Draft -> Open
- Open -> Cancelled -> Open
- Open -> Closed
- Present / Unpresent
- Requested / Quoted
- Revision Confirm
- Withdraw / Expire
- invalid lifecycle transitions
- Quote request reasons

### Application/use-case tests

Use repository mocks/fakes.

Focus on:

- correct domain operation invoked
- correct authorization checks
- correct related objects updated
- expected errors returned
- bulk partial success behavior
- EnsureWorkingQuote behavior

### Infrastructure integration tests

Use **real PostgreSQL**, ideally via Testcontainers.

Focus on:

- EF mapping
- migration
- transaction atomicity
- unique/partial indexes
- optimistic concurrency
- JSONB Event serialization/deserialization
- queries
- expiry worker selection/transition behavior

Do not rely on EF InMemory provider as a substitute for PostgreSQL behavior.

### API tests

Cover representative:

- happy paths
- validation
- 403
- 404
- 409 conflict
- CalculationFailure mapping
- OpenAPI generation smoke test

### FE tests

Focus on important behavior, not every component:

- grid edit -> expected API
- calculation failure reverts cell
- conflict reload behavior
- Refresh discards FE-only edits
- Pending / Last Refresh change windows
- permission-based disable/availability
- Grid config restore

### E2E

Keep a small number of representative business journeys:

1. Sales creates RFQ -> Trader quotes -> Sales presents -> Hit
2. Sales revises RFQ -> Trader requotes -> Away
3. Quote expires -> Requested/Expired -> requote
4. Cancel -> Reopen -> requote
5. WorkingQuote conflict / Revision conflict

---

## 2. Mock/master strategy

Initial implementation uses mock/seed master data.

Prepare only what the app needs.

### User

Fields:

- UserId
- Name
- roles
- Desk

Include several Sales, Traders, and optionally Manager-shaped records.

### Desk

A few fixed desks are sufficient.

### Category

A small realistic set is sufficient, e.g. JGB / Corporate / Other or desk-appropriate categories.

### Routing

Simple:

```text
Category -> Default Trader
```

### Client

Tens of fictional/seed clients are sufficient.

### Security

Use realistic data because security-search behavior matters.

---

## 3. Security seed from JSDA reference-price data

Use the publicly available Japanese bond reference-price/security list data as a convenient realistic source where practical.

Extract useful fields such as:

- bond/security code
- name
- coupon
- maturity

Generate application `SecurityId`.

Add mock fields as needed:

- ISIN
- ticker / BBG-style display
- category

The goal is realistic search/demo data, not a legally authoritative security master.

---

## 4. Mock ISIN

For seed/demo purposes, generate structurally valid JP-prefixed ISIN-like identifiers with valid check digits where convenient.

They do not need to reproduce the real issue's actual ISIN.

The important test properties are:

- full 12-character lookup
- prefix lookup
- alphanumeric support
- checksum path if implemented

---

## 5. Calculation mock data

Do not build fake holiday, curve, convention, or yield-spread master systems inside the RFQ app.

Hide those behind the mock CalculationClient.

The mock may use one deterministic formula across arbitrary securities.

It should produce plausible:

- price
- yield
- simple yield
- spread
- accrued
- settlement amount
- delta

and support controlled errors.

---

## 6. Demo RFQ seed

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
- multiple Contact Owners
- multiple Assigned Traders
- Past RFQ search

Hundreds to a few thousand synthetic historical Cases are enough for early UI/search testing.

Performance testing can generate much larger volumes separately.

<!-- END 08-testing-and-seed.md -->

---

<!-- BEGIN 09-scope-and-deferred.md -->

# 09. Scope, Non-goals, and Deferred Work

## 1. Explicitly out of initial scope

### External inbound channels

No Bloomberg Chat/direct inbound adapter initially.

Keep source extensible, but Manual creation is enough.

### New Bulk / List / Thread / Portfolio workflow

Do not design generic grouping before desk workflow is known.

Core Case design must allow future external grouping without adding parent-case semantics.

### Real calculation server

Initial integration uses MockCalculationClient.

The interface must match the intended bulk request/result behavior.

### Realtime market feed

No realtime market data integration initially.

Calculated quote flow uses mocked/close-based context and manual simple-yield slide.

### Full authentication architecture

Current user/roles can be resolved by the hosting environment.

Transport/mechanism is not specified here.

### Multi-instance App Server

Single instance initially.

No Redis, distributed event fan-out, or leader election.

### Full event sourcing

Events are audit/notification/history.

Current state is stored directly in CaseCurrent.

### Sophisticated Past RFQ paging

Initial cap is approximately 20k returned rows.

Introduce keyset/infinite paging only if real usage needs it.

### Materialized search model

Do not create a large precomputed Past RFQ snapshot until query performance proves necessary.

### Desktop/browser notifications

Initial important notifications are in-app toasts.

Desktop Notifications API can be added later.

### Manager workflow

Manager role can be represented and authorization centralized, but manager-specific overrides are deferred.

### Pricer apply-back

Pricer is independent scratch state initially.

Official WorkingQuote apply-back is deferred.

---

## 2. Deliberately simplified initial assumptions

- Side = Customer Sell
- Security Type = Bond
- Contact Owner exists
- Assigned Trader exists by Confirm
- one Draft Revision maximum per Case
- one WorkingQuote maximum per Revision
- Quote expiry policy = None or N minutes
- expiry check interval ≈ 10 seconds
- no auto-Away at EOD
- no automatic UI refresh of main RFQ data
- security/client search hits DB directly without broad query-result caching

---

## 3. Future extensions the design should tolerate

- List / Thread / Bulk creation
- external RFQ adapters
- real calculation server
- realtime market snapshots
- multiple active quote variants
- richer quote lifecycle/read model
- Manager/admin overrides
- multiple application instances
- distributed notification transport
- full browser/desktop notifications
- paging / large-history optimization
- dedicated search projection
- event archival/partitioning
- richer expiry policies such as AM/PM/EOD
- complete calculation attempt audit
- pricer -> WorkingQuote apply-back
- external manager/master services

---

## 4. Known implementation-sensitive areas

These should be validated during implementation rather than over-specified now:

- exact EF Core mapping shape for lifecycle-specific nullable columns
- exact serialization schema for WorkingQuote/CalculationContext payloads
- endpoint URL naming
- OpenAPI DTO naming
- SSE behavior through the real reverse proxy/load balancer
- exact existing-user authentication integration
- practical Security search index strategy
- ag-Grid keyboard shortcuts
- exact Sales screen lower-panel contents
- actual EOD desk operating procedure
- manager override policy

The design intentionally leaves these adaptable.

<!-- END 09-scope-and-deferred.md -->

---

<!-- BEGIN 10-design-decisions.md -->

# 10. Design Decisions and Rationale

This document records the non-obvious design decisions that materially affect implementation.  
The goal is not to preserve every discussion detail, but to preserve enough rationale that a later implementation does not casually collapse or reinterpret important boundaries.

---

## 1. Separate `RfqStatus`, `QuoteStatus`, and `QuoteRequestReason`

The workflow originally looked like it might need a single large status enum containing states such as:

```text
Active
Quoted
Presented
Amending
Requote
Cancelled
Closed
...
```

That creates composite-status growth because several dimensions are independent.

The chosen split is:

```text
RfqStatus
- Draft
- Active
- Presented
- Cancelled
- Hit
- Away
```

```text
QuoteStatus
- Requested
- Quoted
```

```text
QuoteRequestReason
- Initial
- Revised
- Reopened
- Expired
- Withdrawn
```

Rationale:

- RFQ/customer-facing state and trader-quote workflow are different dimensions.
- `Requote` is not really a stable trader state; it is a reason the quote is Requested again.
- `Hit` and `Away` are useful business-facing terminal statuses and make a separate generic `Closed` display status unnecessary.
- This split avoids combinations such as `AmendingQuoted`, `AmendingPresented`, etc.

---

## 2. Amendment is a Draft Revision, not an RFQ status

Do not introduce `Amending` as an RFQ lifecycle status.

An amendment is represented by:

```text
current confirmed Revision
+
one additional Draft Revision
```

While the Draft exists:

- the current confirmed Revision remains authoritative
- the current quote may remain valid
- trader/customer statuses do not change merely because Sales is editing
- Sales can highlight Draft differences separately

Only when the amendment is Confirmed does the RFQ transition to:

```text
RfqStatus = Active
QuoteStatus = Requested
QuoteRequestReason = Revised
```

Rationale:

- "Sales is editing a future Revision" is not the same kind of state as "customer-facing RFQ is Presented".
- Treating it as a separate status creates unnecessary cross-products with Quoted/Presented.
- Revision itself already carries the correct temporal meaning.

---

## 3. Use coarse lifecycle types only

The domain may use a typed lifecycle such as:

```text
Draft
Open
Cancelled
Closed
```

but should not encode every operational combination into distinct types.

Inside `Open`, fields such as:

- `RfqStatus`
- `QuoteStatus`
- `QuoteRequestReason`
- ownership
- Contact Owner
- Assigned Trader

remain ordinary values.

Rationale:

- Typed lifecycle prevents category errors such as Presenting a Closed RFQ.
- Encoding every orthogonal combination as a type would create combinatorial explosion and make persistence awkward.
- The lifecycle boundary is the useful level of compile-time safety.

---

## 4. `ConfirmedQuote` is immutable

A ConfirmedQuote is the trader-confirmed pricing snapshot at a particular point in time.

It is not rewritten when later lifecycle events occur.

Events such as:

- Presented
- Unpresented
- Withdrawn
- Expired

are represented separately.

Rationale:

- Confirmed values must remain historically reproducible.
- Mutable lifecycle flags on the quote would blur "what was confirmed" with "what happened later".
- Immutable snapshots make search/history and audit easier to reason about.

---

## 5. Current operational state lives in `CaseCurrent`

The system is **not** full event sourcing.

Current state is stored directly in a mutable projection:

```text
CaseCurrent
```

Events are also appended for history and notifications.

Rationale:

- Normal UI/search needs fast access to the current state.
- Reconstructing every Case from all historical events would add complexity without current business value.
- Event history is useful for audit, notification, and investigation, but is not the sole authoritative representation of current operational state.

---

## 6. Persistence event hierarchy does not imply domain inheritance

Persistence uses:

```text
Event
├─ RfqEvent
└─ QuoteEvent
```

The shared parent exists primarily so that:

- all events share a global `EventId`
- notification/reconnect cursor is one-dimensional
- common fields such as occurrence time/actor are stored once conceptually

However, the domain does **not** need a shared Event base class/DU.

Rationale:

- `CaseId` belongs logically to RFQ events.
- `QuoteId` belongs logically to Quote events.
- The common persistence envelope is useful infrastructure, but not necessarily a meaningful domain abstraction.

---

## 7. Event payloads use typed domain values + JSONB persistence

Avoid either extreme:

- one huge nullable event table
- one physical table per event type

Use:

```text
RfqEvent / QuoteEvent
- Type
- Payload jsonb
```

The domain represents event payloads with typed cases/records.

Repository/infrastructure serializes those typed payloads to JSONB.

Rationale:

- Event types will evolve.
- Large nullable schemas become difficult to maintain.
- Table-per-event-type is too heavy for the current scale.
- JSONB keeps persistence flexible while domain types remain explicit.

---

## 8. Expiry is a real business transition, not only a visual warning

Expiry was considered as a UI-only `Review` badge.

The chosen design is stronger:

```text
Quoted -> Requested
QuoteRequestReason = Expired
Presented -> Active, if needed
```

executed by a background worker.

Rationale:

- If expiry is only visual, every write operation must independently remember to reinterpret "Quoted but expired".
- A real state transition makes permissions and subsequent workflow consistent.
- RFQ is sufficiently time-sensitive that automatic expiry is valuable.

Initial implementation still keeps expiry mechanics small:

- same App Server process
- `BackgroundService`
- about 10-second interval
- idempotent transition
- single App Server assumption

---

## 9. Expiry is its own event, even if mechanics overlap with Withdraw

Internally, Expire and Withdraw may reuse common "deactivate current quote / request quote again" logic.

Business history should still record:

```text
Expired
```

rather than synthesizing:

```text
Unpresented
Withdrawn
```

Rationale:

- human actions and time-driven expiry have different business meanings
- audit/history should not imply a user manually performed actions that were automatic

---

## 10. WorkingQuote belongs to Revision

A WorkingQuote is not merely a Case-level scratch area.

It belongs to a Revision.

Rationale:

- quote inputs/results are meaningful against a specific RFQ condition set
- historical revisions may need their old working values when customers return to previous terms
- revision changes should create/seed a new WorkingQuote rather than mutate historical quote work

Confirmed Revision invariant:

```text
Confirmed Revision -> WorkingQuote exists
```

implemented through `EnsureWorkingQuote`.

---

## 11. `EnsureWorkingQuote` is both invariant enforcement and recovery

At Revision Confirm:

- use existing WorkingQuote if present
- otherwise clone configured seed WorkingQuote
- otherwise create empty/default WorkingQuote

Trader access may defensively invoke the same behavior if data is missing.

Rationale:

- an invariant that can only fail permanently is operationally fragile
- the system can cheaply self-heal this particular missing object
- concurrency should still be protected by uniqueness/version rules

---

## 12. Reopen does not resurrect quotes, but restores working values

Reopening a Cancelled RFQ results in:

```text
RfqStatus = Active
QuoteStatus = Requested
QuoteRequestReason = Reopened
```

The old confirmed quote is not reactivated.

WorkingQuote values remain available.

Rationale:

- a previously communicated quote may no longer be valid after cancellation time passed
- forcing re-confirmation makes the trader consciously accept the quote again
- retaining working values avoids unnecessary re-entry

---

## 13. Revision update invalidates the old active quote only on Confirm

While Sales edits a Draft amendment, the previous confirmed RFQ and quote remain operational.

On Revision Confirm:

```text
QuoteStatus -> Requested
Reason -> Revised
Presented -> Active, if needed
```

Rationale:

- Draft edits are not yet agreed/official RFQ conditions
- invalidating a quote as soon as Sales starts typing would make the workflow unstable
- confirmation is the correct atomic boundary

---

## 14. Presentation is customer-workflow state, not trader-pricing state

`Presented` belongs conceptually with the RFQ/customer-facing side.

It means the Contact Owner has protected the current quote from trader withdrawal while it is customer-facing.

It does **not** prove the customer literally saw it.

Rationale:

- the operation is controlled by Contact Owner
- the key behavior is withdrawal protection, not pricing calculation
- presentation does not change the trader's quote from Quoted to another trader state

---

## 15. Do not auto-refresh active grids

Server-side changes should not silently replace data in an integrated grid/editor.

Instead:

```text
server change
-> notify "updates available"
-> user chooses Refresh
-> authoritative state is reloaded
```

FE-only uncommitted edits are discarded on Refresh.

Rationale:

- the same screen is both a live list and editing surface
- invisible replacement of cells while a user is working is hard to reason about
- explicit refresh gives users control over context changes

---

## 16. Keep two change windows

The Changes UI keeps:

```text
Last Refresh
Pending Updates
```

rather than clearing change information on Refresh.

Rationale:

- if Refresh immediately erased the only change list, users could not see what they just incorporated
- two generations are sufficient operationally
- long-term audit remains in Event tables, so FE does not need a permanent change history

---

## 17. SSE is a wake-up channel, not the data source

SSE sends a lightweight indication that something changed.

FE then calls the persisted event query:

```text
GetEventsAfter(lastSeenEventId)
```

Rationale:

- reconnect/catch-up is simpler
- persisted Events remain the reliable source of change information
- transport can later fall back to polling without changing domain/event design
- SSE does not need to carry complete authoritative UI payloads

---

## 18. Separate normal update indication from important toast events

Most changes should only cause:

```text
Updates Available
```

Only important state transitions produce immediate in-app toasts.

Rationale:

- desk-wide RFQ systems can generate many changes
- toast storms are worse than slightly delayed list refresh
- state-changing events such as Close/Withdraw/Expiry/TakeOver deserve immediate attention

---

## 19. Lightweight Command / Query separation

Commands use domain rules and repositories.

Queries may directly read/join DB state into view DTOs.

Rationale:

- history/search screens often need wide joins but no domain mutation logic
- reconstructing a full aggregate for search is wasteful
- update paths still need domain invariants and atomic transactions
- this is a practical split, not an attempt to build a full CQRS platform

---

## 20. Repository abstractions should not expose DB-column operations

Application code should not primarily manipulate persistence concepts such as:

```text
SetCurrentQuoteId(null)
InsertEventRow(...)
SetStatusColumn(...)
```

Use application/domain operations such as:

```text
ConfirmRevision
ConfirmQuote
WithdrawQuote
ExpireQuote
CloseCase
```

and persist resulting state through repositories/unit of work.

Rationale:

- keeps business rules out of infrastructure
- allows persistence structure to change without rewriting the application workflow
- avoids leaking EF/DbContext concepts across layers

---

## 21. Use real PostgreSQL for infrastructure tests

Application/domain tests may mock repositories.

Repository/transaction/query tests should use real PostgreSQL, preferably via Testcontainers.

Rationale:

- PostgreSQL-specific features matter here:
  - partial indexes
  - JSONB
  - transaction behavior
  - concurrency
- EF InMemory/SQLite substitutes can hide the failures that matter most

---

## 22. Keep the first Past RFQ search intentionally simple

Initial design:

- server query
- indexes
- approximately 20k row cap
- FE client-side grid sort/filter
- user narrows search if too broad

Do not implement complex paging/projection/materialized-view infrastructure before it is needed.

Rationale:

- expected total size (~hundreds of thousands of Cases) is small for PostgreSQL
- business query patterns are not fully known yet
- premature search infrastructure would lock in assumptions

---

## 23. Master data and pricing realism are intentionally mocked

The RFQ app should not reimplement:

- holidays
- market conventions
- curves
- pricing conventions

Those belong behind the calculation boundary.

Initial seed/master exists only to make the application usable and searchable.

Rationale:

- this project is validating RFQ workflow, not rebuilding the firm's financial calculation stack
- keeping fake pricing complexity out of the RFQ app makes replacement by the real calculation service straightforward

---

## 24. Authorization logic is centralized because roles will evolve

Business authorization must pass through one policy/service boundary.

Rationale:

- checks depend on several dimensions:
  - Role
  - Desk
  - Contact Owner
  - Assigned Trader
  - Owned
  - future Manager override
- scattering these predicates across endpoints/handlers will become unmaintainable
- future Manager behavior should be added centrally rather than patched into every use case

---

## 25. Unknown business workflows are deliberately deferred

Examples:

- New Bulk
- List / Thread / Portfolio grouping
- Bloomberg inbound format
- Manager overrides
- richer EOD operations
- richer Pricer integration

Rationale:

- desk workflow is not yet known well enough
- the core Case/Revision/Quote design should leave room for these without inventing speculative abstractions now

The implementation should prefer replaceable boundaries over guessed feature behavior.

---

## 26. Persisted change feed must cover cross-session observable transitions

The event feed is the recovery/source-of-truth mechanism for change notification. Therefore any business transition that another active session must be able to discover through Pending Updates must append a persisted event in the same transaction as the state mutation.

At minimum this includes quote confirmation (`Requested -> Quoted`) in addition to Revision Confirm, lifecycle transitions, ownership/responsibility transitions where notification is required, and Presentation/Withdraw/Expire.

Rationale:

- SSE carries only a wake-up signal.
- A state transition without a persisted event can be silently invisible after reconnect.
- Event coverage should be decided by cross-session observability, not by whether the transition feels like an audit event.

---

## 27. Event cursor ordering must follow commit visibility

`GetEventsAfter(lastSeenEventId)` must not rely on a plain identity/sequence allocation order when concurrent event-producing transactions can commit out of order.

The infrastructure must use a commit-order-safe cursor mechanism and prove it with a concurrency integration test where two event-producing transactions obtain/prepare events in one order but commit in the opposite order.

Rationale:

- Advancing a cursor past an uncommitted lower EventId can permanently hide that event after it commits.
- The persisted event feed is explicitly required to provide reconnect recovery without silent data loss.

<!-- END 10-design-decisions.md -->
