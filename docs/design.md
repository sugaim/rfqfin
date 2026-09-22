# JPY Corporate Bond RFQ System — Canonical Design

This file is the **single canonical design document** for the internal JPY corporate bond RFQ system.

It is updated in place. There is no independent document version number: **Git history is the version history**. Historical implementation instructions, refactoring plans, and change notes are intentionally not part of the active documentation set.

This document records current intended behavior and the design rules that should survive implementation changes. It is not an implementation diary and does not prescribe a historical sequence of changes.

## 1. System purpose and business boundary

This application supports an internal JPY corporate-bond RFQ workflow.

At a high level:

- Sales creates/manages the customer inquiry and customer-contact workflow.
- Trader owns quote work and calculation/confirmation operations when authorized.
- Contact Owner owns customer-facing presentation and normal Hit/Away closure.
- Post Process supports operational cleanup, same-Business-Date outcome correction, and own-side memo maintenance.
- The RFQ workflow ends at RFQ outcomes such as Hit, Away, or Cancelled.
- **Hit is not Booking.** Booking/ticket creation and booking reconciliation are separate downstream concerns.
- Detailed bond pricing, curve/convention logic, and standard-settlement calculation belong behind the Calculation boundary rather than inside the RFQ workflow application.

The system is intentionally an RFQ workflow application, not a generic workflow engine, OMS, booking system, or pricing library.

## 2. Architecture at a glance

```text
React / TypeScript browser
        |
        v
ASP.NET Core API
        |
        v
Rfq.Application  (Use Cases)
        |
        +----> Rfq.Domain
        |
        +----> ports / abstractions
                 |
                 +----> PostgreSQL / EF Core infrastructure
                 +----> Calculation service/client
                 +----> event / time / identity infrastructure
```

Dependency direction and responsibility matter more than the physical diagram:

- Domain contains valid business state and deterministic business transformations.
- Application orchestrates use cases around Domain and external/system concerns.
- API maps transport contracts to/from Application.
- Infrastructure implements persistence and other external ports.
- React owns presentation and FE-local interaction state, not business authority.

## 3. Non-negotiable design rules

These rules are the fastest way to understand what must not be casually refactored away.

1. **Domain data represents valid business state.** Important invalid RFQ/quote combinations should be unrepresentable through public construction where practical.
2. **Business state is immutable from callers.** State changes return new state through explicit Domain transitions/factories.
3. **Domain/Application boundary is decided by invariants and coherency, not by object count.** An operation belongs in Domain when splitting it into independently callable pieces would permit an invalid business state. Touching multiple objects does not by itself make something Application logic.
4. **Data and business operations are deliberately separated.** Business transitions are generally explicit operation/transition functions rather than mutable entity member methods. Factories, validation, and natural value-object operations may still live on the type.
5. **Application is the Use Case layer.** It owns loading, authorization, ID/time/Business-Date resolution, external calls, persistence, event append, and transaction orchestration.
6. **State validity and actor authorization are different concerns.** Domain owns state validity; Application owns current-user/role/desk policy.
7. **Business Date is a first-class business fact.** Persist it where semantics depend on the desk day; do not reconstruct operational day from UTC timestamps.
8. **Use optimistic concurrency.** Do not hold long-lived DB locks around user work or external calculation; re-read/revalidate after external work before applying results.
9. **Bulk APIs remain operation-specific and reuse single-item business logic.** Bulk is normally per-Case atomic with explicit partial success; unexpected/invariant failures are not silently converted into ordinary item failures.
10. **Persistence shape may differ from Domain shape.** Current operational state is stored directly; persisted Events are audit/notification/history, not event sourcing.
11. **SSE is a wake-up mechanism, not authoritative state.** Authoritative state comes from normal queries/persisted data.
12. **Calculation does pricing; RFQ Application owns RFQ workflow.** Calculation must not become the owner of lifecycle, ownership, WorkingQuote persistence, or RFQ concurrency.
13. **Server query state is authoritative; local interaction state is separate.** The frontend must not reconstruct business transitions after mutations. Live/Paused reconciliation uses authoritative page projections, while short-lived protected input is not silently overwritten.
14. **This is a dense operational desktop tool.** Information density, inline/grid editing, keyboard efficiency, and low-friction repeated actions are product requirements; confirmations should be proportional to risk rather than applied everywhere.
15. **Semantic personal settings are typed.** Theme, Default Quote Mode, and Quote Expiry use typed contracts; grid layout alone is intentionally frontend-owned opaque JSON.
16. **Source organization follows ownership, not framework artifact type.** Backend/Application code groups by business/use-case ownership. Frontend workspace code is page-owned under `pages/<page>`; `shared` is for genuinely cross-page concepts, and `features` is reserved for independent cross-page user features rather than being another name for a page.

## 4. Documentation rule

Only this file under `docs/` is active design documentation.

Code remains authoritative for mechanical implementation details that carry no design meaning, such as a private helper name. This document is authoritative for business meaning, invariants, responsibility boundaries, and deliberate architectural/UX rules. If code and this document diverge on those matters, reconcile the inconsistency rather than treating an old implementation instruction as authority.

---

# 01. Domain Model

## 1. RFQ Case identity and current shape

An **RFQ Case** represents one customer inquiry that ultimately closes with one Hit/Away outcome.

Boundary rule:

- if a new condition **replaces** a previous condition, it is the same Case with a new Revision
- if two conditions can coexist and independently result in Hit/Away, they are separate Cases
- future List / Thread / Portfolio grouping lives outside the Case

Changing Client or Security means creating a new Case, not a Revision.

Current Domain shape:

```text
RfqCase
- CaseId
- ClientId
- SecurityId
- CategorySnapshot : CategoryId
- CreatedAt
- CreatedBusinessDate?
- CreatedBy
- SalesId?
- ContactOwnerId
- AssignedTraderId
- Version : StateVersion
- CurrentRevision : RfqRevision
- Lifecycle : RfqLifecycle
- PendingDraftRevision?
- CopiedFromCaseId?
```

Useful status/quote/close/ownership properties are derived from the typed lifecycle rather than maintained as a second mutable source of truth:

- `Status`
- `QuoteStatus?`
- `QuoteRequestReason?`
- `CurrentQuoteId?`
- `ClosedQuoteId?`
- `ClosedBusinessDate?`
- `Ownership?`

`ContactOwnerId` and `AssignedTraderId` are Case-level values. Do not duplicate them inside lifecycle subtypes.

### CaseId semantics

`CaseId` is an internal typed identity backed by PostgreSQL `bigint identity`.

It is **not** a gapless business sequence or a human-facing business number:

- gaps are allowed after failed/rolled-back allocation and normal DB behavior
- the value must not be interpreted as an exact count of Cases
- internal ordering/identity convenience does not make it a contractual display number
- a separate human-facing case/reference number may be added later without replacing the internal CaseId

---

## 2. Immutable Domain data

Business-state objects are immutable from callers.

In particular:

- `RfqCase`
- `RfqRevision`
- `WorkingQuote`
- `ConfirmedQuote`
- `SalesMemo`
- `TraderMemo`
- lifecycle/state values

must not expose mutating public methods/setters.

Business state changes are performed by Domain transition functions/classes and return new values.

Construction/factory methods may live on the data type where they create a valid new object. Persistence rehydration is separate and must not become a general public escape hatch.

Everything does not need to be a record; a sealed class with get-only properties is acceptable.

---

## 3. Lifecycle types

The Domain source of truth is typed lifecycle state:

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
   ├─ HitRfq
   └─ AwayRfq
```

### `DraftRfq`

Initial RFQ exists but has not been confirmed/opened.

### `OpenRfq`

Abstract base for open workflow. It contains state common to Open RFQs, including:

- current Revision identity
- trader `Ownership`

Open has exactly two customer-facing forms:

- `ActiveRfq`
- `PresentedRfq`

### `ActiveRfq`

Carries exactly one `ActiveQuoteState`:

```text
QuoteRequested(reason)
QuoteConfirmed(quoteId)
```

### `PresentedRfq`

Requires a current `QuoteId` by construction. Presented therefore always means a valid confirmed current quote exists.

### `CancelledRfq`

Temporarily terminal; may be reopened. Old ConfirmedQuote is not resurrected on reopen.

### `ClosedRfq`

Abstract terminal state carrying common close data, including:

- `ClosedQuoteId`
- `ClosedBusinessDate`

Concrete terminal states are:

- `HitRfq`
- `AwayRfq`

Hit/Away are therefore represented by state type, not by passing an outcome enum into a generic closed state.

Cancelled is deliberately not a `ClosedRfq`: it is reopenable and has different semantics.

---

## 4. Boundary status concepts

Business/UI/persistence projections still use:

### `RfqStatus`

```text
Draft
Active
Presented
Cancelled
Hit
Away
```

### `QuoteStatus`

```text
Requested
Quoted
```

### `QuoteRequestReason`

```text
Initial
Revised
Reopened
Expired
Withdrawn
```

However, `RfqStatus` / `QuoteStatus` are **not a second Domain source of truth**. Derive them from the typed lifecycle/quote-state model when mapping to DB/API/query DTOs.

`QuoteRequestReason` remains real Domain data as the payload of `QuoteRequested`.

---

## 5. Ownership

Trader ownership is typed rather than a Domain boolean:

```text
Ownership
├─ Unowned
└─ Owned
```

`Ownership` exists only while the RFQ is Open.

`Owned` means the owner is the Case-level `AssignedTraderId`; no separate owner ID is needed.

Operations include:

- Pick Up
- Release
- Assign to...
- Take Over

These operations do not by themselves change the customer-facing RFQ lifecycle or quote state.

Persistence may store this as a boolean projection.

---

## 6. Revision model

A Revision owns the customer condition set.

Current Domain shape:

```text
RfqRevision
- RevisionId
- CaseId
- Status
- Terms : RevisionTerms
- CopiedFromRevisionId?
- QuoteSeedRevisionId?
- Version : StateVersion
- CreatedAt
- CreatedBy
- ConfirmedAt?
- ConfirmedBy?

RevisionTerms
- Notional?
- SettlementDate?
- StandardSettlementDate
- SalesAndTradingMessage
```

Draft terms may be incomplete where the workflow allows it; confirmation performs stronger validation.

Revision status:

```text
Draft
Confirmed
Superseded
Discarded
```

Rules:

- at most one Draft Revision per Case
- initial RFQ Draft and amendment Draft both use `Draft`
- while Case is Draft, the Draft Revision is the current initial draft
- while Case is Open, an additional Draft Revision is a pending amendment
- confirming an amendment:
  - old current Confirmed Revision -> `Superseded`
  - Draft -> `Confirmed`
  - Case `CurrentRevision` becomes the newly confirmed Revision
- discarding a Draft -> `Discarded`
- old Revisions are never reactivated

Revision provenance:

- `CopiedFromRevisionId?` records copied conditions
- `QuoteSeedRevisionId?` explicitly identifies a historical quote seed where applicable

The Case property must be named according to its semantics: use `CurrentRevision`, not a mutable property called `InitialRevision` that later contains amendments.

---

## 7. WorkingQuote

A `WorkingQuote` belongs to exactly one Revision.

Logical cardinality:

```text
Revision -> 0..1 WorkingQuote
```

Persisted operational intent is that a confirmed/current Revision has one WorkingQuote.

Current Domain shape:

```text
WorkingQuote
- RevisionId
- Mode : Calculated | Manual
- Calculated?
- Manual?
- Version : StateVersion
- CreatedAt
- CreatedBy
- UpdatedAt
- UpdatedBy
```

Calculated payload:

```text
CalculatedQuotePayload
- Driver
- DriverValue
- Price
- BbgYield
- BaseSimpleYield
- SimpleYieldSlide
- FinalSimpleYield
- InternalYield
- GSpread
- Asw
- Ysc
- ISpread
- ZSpread
```

Manual payload:

```text
ManualQuotePayload
- Price?
- FinalSimpleYield?
```

Current calculation drivers are:

```text
Price
BbgYield
SimpleYield
Ysc
GSpread
Asw
ISpread
ZSpread
```

`WorkingQuote` is immutable Domain data and is changed only through `WorkingQuoteTransitions`.

It remains:

- versioned using `StateVersion`
- the last successful calculated state once populated
- retained across Withdraw, Expire, Cancel, and Reopen
- attached to its historical Revision

Switch Calculated -> Manual:

- Manual values start empty for the mode switch semantics
- Calculated values remain retained separately

Switch Manual -> Calculated restores the retained Calculated state.

### WorkingQuote factory

WorkingQuote creation is performed by a Domain factory, not by mutating a repository row.

For initial Confirm, Application calls the lifecycle transition, then creates the initial WorkingQuote for the confirmed/current Revision, and persists both in one transaction.

For amendment Confirm, create a new WorkingQuote for the new Revision; seed from `QuoteSeedRevisionId` when specified. Never reuse/reactivate the old Revision's WorkingQuote as the new Revision object.

The DB still enforces one WorkingQuote per Revision.

---

## 8. ConfirmedQuote

A `ConfirmedQuote` is an immutable snapshot created by the **Quote Confirm Domain transition**.

Logical cardinality:

```text
Revision -> 0..N ConfirmedQuote
```

Current Domain shape:

```text
ConfirmedQuote
- QuoteId
- RevisionId
- SecurityId
- SettlementDate
- ConfirmedBy
- ConfirmedAt
- Mode : Calculated | Manual
- Calculated?
- Manual?
- ExpiryMinutes?
- ExpiresAt?
- RequestReasonAnswered
```

The calculated/manual payloads are snapshot copies of the corresponding WorkingQuote payload.

The confirmation input uses typed expiry semantics; the ConfirmedQuote keeps the resolved/current representation required for historical quote behavior (`ExpiryMinutes?` and `ExpiresAt?`).

`ConfirmedQuote` is not independently made current by Application code. Quote confirmation must create the snapshot and update RFQ current quote state as one coherent Domain operation.

It does not mutate when later Presented, Withdrawn, Expired, Hit, or Away.

---

## 9. Quote confirmation metadata and expiry

`QuoteConfirmation` is a Domain value describing the confirmation act:

- `ConfirmedBy : UserId`
- `ConfirmedAt : DateTimeOffset`
- typed expiry policy

`QuoteId` is separate: it is the identity of the new ConfirmedQuote.

Current supported expiry policies are:

- no expiry
- expire after a positive duration

The type should allow future policies such as fixed time / AM / PM / EOD without implementing them now.

Persistence may keep the current `expiry_minutes` plus resolved `expires_at` representation for the currently supported cases.

---

## 10. Calculated and Manual modes

### Calculated

The edited quote cell determines the driver. Current/future supported examples include:

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

The exact metric set may evolve, but requests are typed rather than a generic untyped number.

`SimpleYieldSlide` is independent of the driver.

Initial business logic remains close-based:

- previous-business-day close reference state is the baseline
- calculation produces base simple yield
- current-day simple-yield slide is applied
- final simple yield = base simple yield + slide

Do not generalize the slide into an arbitrary spread adjustment.

### Manual

Manual confirmation requires:

- Price
- Final Simple Yield

They are independently entered and need not be internally consistent.

---

## 11. Contact Owner

`ContactOwnerId` means the person currently responsible for customer contact and Hit/Away closure.

It may be Sales or Trader.

Initial values:

- Sales-created RFQ: Sales = creator, Contact Owner = creator
- Trader-created RFQ: Sales may be absent, Contact Owner = creator

The current Contact Owner may hand off to another eligible Sales/Trader through an explicit Domain transition plus Application authorization.

Primary responsibilities:

- edit/confirm/discard Revision Drafts
- Present / Unpresent
- Hit / Away Close
- outcome correction
- Contact Owner handoff

---

## 12. Category and routing

Category is **master data**, not a compile-time enum.

Logical master:

```text
Category
- CategoryId   // stable key
- Name         // display name, may change
```

References use `CategoryId`; changing `Name` must not break existing Security/routing/history references.

Routing:

```text
Security -> CategoryId -> Default Assigned Trader
```

- Security -> Category may be DB/master dependent
- Category -> Default Trader is editable routing configuration
- Category is snapshotted on Case creation as `CategoryId`
- existing Cases do not auto-reroute when master/routing changes
- Sales may override Assigned Trader before initial confirm

Domain/Application use typed `CategoryId`, never arbitrary category-name strings.

---

## 13. Case memos and message

### Sales & Trading Message

- Revision-owned
- visible to Sales and Trader
- changing it on a confirmed RFQ creates/updates an amendment Draft

### Sales Memo / Trader Memo

They are separate Case-owned versioned Domain objects:

```text
SalesMemo
- CaseId
- Value
- Version

TraderMemo
- CaseId
- Value
- Version
```

Rules:

- independent of Revision
- editable after Close where authorization allows
- immutable from callers
- updated through `SalesMemoTransitions` / `TraderMemoTransitions`
- memo changes are not RFQ lifecycle transitions

This separation preserves own-side semantics without a generic mutable memo bag.

---

## 14. IDs

Use typed IDs in Domain/Application:

- `CaseId`
- `RevisionId`
- `QuoteId`
- `ClientId`
- `SecurityId`
- `CategoryId`
- `UserId`

Allocation responsibility:

- `CaseId`: DB/Application-Infrastructure sequence path
- `RevisionId`: Application allocates and passes into Domain
- `QuoteId`: Application allocates and passes into Domain

Domain transitions/factories should not call random/clock infrastructure internally.

---

## 15. StateVersion

Versioned business state uses:

```text
StateVersion(long)
```

Rules:

- signed `long`
- value >= 1
- checked `Next()`

Use for Case current state, Revision, WorkingQuote, SalesMemo, and TraderMemo.

Infrastructure/API may map to raw `long` at boundaries.

---

## 16. Business Date facts

Business Date is a first-class operational fact and is not reconstructed from UTC timestamps.

Authoritative business date comes from the configured business-date provider and is represented as `DateOnly` at the Application/Domain boundary where applicable.

Persisted facts include:

- `CreatedBusinessDate` on the Case, established when an initial Draft is first confirmed/opened
- `ClosedBusinessDate` on Hit/Away closed lifecycle state
- `BusinessDate` on persisted RFQ events that are used for day-scoped operational queries

Rules:

- Draft Cases do not yet require `CreatedBusinessDate`
- non-Draft Cases do
- Hit/Away require `ClosedBusinessDate`
- outcome correction does not rewrite the original close date
- same-day outcome correction is allowed only when current Business Date equals the original `ClosedBusinessDate`
- timestamps remain audit facts; Business Date remains the operational desk-day fact

Do not infer these facts from `CreatedAt.UtcDateTime.Date` or timestamp ranges.

---

## 17. Create New from Existing

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

Initialize responsibility/routing as a new Case:

- Contact Owner = creator
- Sales = creator if creator is Sales, otherwise absent
- Assigned Trader = rerun routing

Settlement rule:

- source Case created on the current **desk/business date** -> copy source actual settlement
- older source Case -> use current standard settlement flow

The source-created date comparison must use the configured desk/business timezone, not `CreatedAt.UtcDateTime.Date`.

Store `CopiedFromCaseId?` as provenance only.


---

# 02. State Transitions

## 1. Transition organization

Domain transitions are grouped by **business transition category**.

The classification is not based on how many Domain objects are read or returned.

Representative transition groups:

```text
InitialDraftTransitions
RfqLifecycleTransitions
RfqOwnershipTransitions
RfqResponsibilityTransitions
QuoteTransitions
AmendmentTransitions
WorkingQuoteTransitions
SalesMemoTransitions
TraderMemoTransitions
```

The grouping follows business transition meaning. It is not determined by how many Domain objects the transition consumes or returns.

Use an explicit transition for Contact Owner handoff and for initial-Draft editing; do not fall back to public mutable setters or a generic public Revision transition that bypasses Case rules.

Application loads state, authorizes, supplies IDs/time/business date, invokes transitions/factories, persists all results, records events, and commits atomically.

---

## 2. Initial creation

### Unsaved New

Pressing `New` creates only FE-local state.

No Case ID is created until Save Draft or Confirm.

Closing an unsaved New form discards it without persistence.

Unsaved New input is not authoritative server state and does not suspend Live refresh of the existing RFQ grid.

### Save Draft

Minimum required:

- resolved Client
- resolved Security

Effects:

- create Case
- create initial Revision with `Draft`
- lifecycle = `DraftRfq`
- Case ID assigned
- the persisted Draft becomes the server-side working copy
- subsequent field edits autosave through an explicit initial-Draft Domain transition when editing completes and the effective value actually changed

### Initial Confirm

Validation includes:

- Client resolved
- Security resolved
- Notional > 0
- valid Settlement Date
- Settlement Date >= business `today`
- Contact Owner set
- Assigned Trader set

`today` is desk/business date, not UTC calendar date.

Domain transition effects:

```text
DraftRfq -> ActiveRfq
Draft Revision -> Confirmed
Active quote state = QuoteRequested(Initial)
Ownership = Unowned
```

WorkingQuote creation is a separate Domain factory call in the same Application transaction:

```text
confirmed/current Revision -> initial WorkingQuote
```

Persist the RFQ/Revision/WorkingQuote atomically.

---

## 3. Quote Confirm

Preconditions include:

- lifecycle is `ActiveRfq`
- active quote state is `QuoteRequested`
- WorkingQuote belongs to current Revision
- expected Case and WorkingQuote versions match where supplied
- quote inputs are valid
- required calculation context is present

Actor authorization (owning trader etc.) is enforced by Application authorization, not by role lookup in Domain.

The Domain quote-confirm operation is coherent:

```text
ActiveRfq(QuoteRequested(reason))
+ current WorkingQuote
+ new QuoteId
+ QuoteConfirmation
        ->
ActiveRfq(QuoteConfirmed(new QuoteId))
+ immutable ConfirmedQuote snapshot
```

Application must not create these two outcomes independently through unrelated public APIs.

Quote Confirm does not recalculate; it snapshots the already-successful WorkingQuote.

Emit persisted `QuoteEvent: Confirmed` in the same application transaction.

---

## 4. Present / Unpresent

Only the current Contact Owner may perform these operations; that actor check belongs to Application authorization.

### Present

```text
ActiveRfq(QuoteConfirmed(quoteId))
    -> PresentedRfq(quoteId)
```

Presentation means the Contact Owner protects the current quote from trader withdrawal while customer-facing exposure is live.

It is not proof that the customer literally saw the quote.

### Unpresent

```text
PresentedRfq(quoteId)
    -> ActiveRfq(QuoteConfirmed(quoteId))
```

After Unpresent, the trader may Withdraw.

Presentation is not required for Hit/Away Close.

---

## 5. Amendment Draft

A confirmed/open RFQ may have at most one pending amendment Draft Revision.

The Draft may start in either of two intentional ways:

- an inline edit completes with a value different from the effective current value, which creates or updates the pending Draft
- an explicit `Start Amendment` action creates the pending Draft before pane-based editing begins

Once a pending Draft exists, its editable fields are a server-side working copy and subsequent edits autosave when editing completes and the effective value actually changed.

The effective value is the pending Draft value when present; otherwise it is the current confirmed Revision value.

A zero-difference amendment Draft is a valid working state. It is not auto-discarded, cannot be confirmed, and remains until it acquires a real change or is explicitly discarded.

While Draft is being edited:

- current confirmed Revision remains authoritative
- current confirmed quote remains valid
- lifecycle/quote state does not change
- Sales sees changed-cell highlighting
- Trader does not see Draft contents

### Amendment Confirm

Effects:

```text
old current Confirmed Revision -> Superseded
Draft Revision -> Confirmed
Case CurrentRevision -> new Revision
PresentedRfq, if any -> ActiveRfq
current confirmed quote is no longer current
Active quote state -> QuoteRequested(Revised)
```

Create a **new** WorkingQuote for the new Revision in the same Application transaction.

Seed rule:

- normal amendment -> configured previous/current relevant Revision WorkingQuote
- return-to-old-condition -> explicitly selected historical Revision WorkingQuote via `QuoteSeedRevisionId`

Do not reactivate an old Revision or reuse its WorkingQuote object as the new current WorkingQuote.

### Amendment Discard

```text
Pending Draft -> Discarded
```

Current Revision, current quote, and lifecycle remain unchanged.

---

## 6. Withdraw

Trader operation; actor authorization is Application policy.

Allowed only from:

```text
ActiveRfq(QuoteConfirmed(quoteId))
```

Presented is rejected by Domain state precondition.

Effects:

```text
ActiveRfq(QuoteConfirmed)
    -> ActiveRfq(QuoteRequested(Withdrawn))
```

- current confirmed quote is no longer current
- WorkingQuote retained
- ConfirmedQuote history retained
- emit `QuoteEvent: Withdrawn`

---

## 7. Expiry

Current supported policies:

- None
- positive fixed duration

At confirmation, fixed duration resolves `ExpiresAt` from `ConfirmedAt`.

Expiry is a real Quote transition executed by the server worker.

Effects:

```text
ActiveRfq(QuoteConfirmed)
    -> ActiveRfq(QuoteRequested(Expired))

PresentedRfq
    -> ActiveRfq(QuoteRequested(Expired))
```

- current confirmed quote is no longer current
- WorkingQuote retained
- emit exactly `QuoteEvent: Expired`

Do not model expiry as synthetic `Unpresented` + `Withdrawn` business events.

---

## 8. Cancel / Reopen

### Cancel

Allowed from any Open RFQ.

Effects:

```text
OpenRfq -> CancelledRfq
```

- current confirmed quote becomes non-operational
- Open ownership state disappears
- Case-level Assigned Trader retained
- Case-level Contact Owner retained
- WorkingQuote retained
- pending amendment Draft may remain Draft
- ConfirmedQuote history remains

### Reopen

Does not resurrect an old confirmed quote.

Effects:

```text
CancelledRfq
    -> ActiveRfq(
         QuoteRequested(Reopened),
         Ownership = Unowned)
```

Assigned Trader and Contact Owner remain Case-level values.

WorkingQuote values remain available for review/reconfirmation.

Presentation is not restored.

---

## 9. Close: Hit / Away

Only the current Contact Owner may normally Close; actor authorization is Application policy.

Domain precondition:

- Open RFQ has a current confirmed quote
- Presentation is not required

Effects are operation-specific:

```text
CloseHit(OpenRfq, BusinessDate)  -> HitRfq
CloseAway(OpenRfq, BusinessDate) -> AwayRfq
```

Both concrete closed states retain:

- `ClosedQuoteId = current quote`
- `ClosedBusinessDate = current Business Date`

Also:

- pending Draft Revision is automatically Discarded
- WorkingQuote retained as history
- ConfirmedQuote remains immutable
- Case-level Assigned Trader retained
- Case-level Contact Owner retained

Public Application/API operations are explicit `CloseHitRfq` / `CloseAwayRfq`. An internal shared orchestration helper may still dispatch the two operations, but Hit/Away is not modeled as a generic public lifecycle payload.

### Bulk Close

Bulk Hit/Away closes only Cases that are still eligible/open.

Already Hit/Away Cases are skipped; bulk Close never silently changes an existing outcome.

### Outcome correction

Explicit Domain transitions only:

```text
CorrectToAway(HitRfq) -> AwayRfq
CorrectToHit(AwayRfq) -> HitRfq
```

Application additionally requires:

- current Business Date equals the original `ClosedBusinessDate`
- a non-empty trimmed correction reason

The original `ClosedBusinessDate` is preserved. Append an `OutcomeCorrected` event carrying the correction reason and current Business Date.

---

## 10. Trader ownership transitions

### Pick Up

Target: Open + `Unowned`.

- self-assigned -> `Owned`
- assigned to another trader but unowned -> Application confirmation/authorization, then Case AssignedTrader=self and Open Ownership=`Owned`
- other-owned -> not eligible; use Take Over

### Release

Target: Open + `Owned` where Application authorization confirms actor is owner.

```text
Ownership: Owned -> Unowned
AssignedTrader unchanged
```

### Assign to...

Target: Open + `Unowned`.

```text
AssignedTrader = target
Ownership = Unowned
```

### Take Over

Target: Open + `Owned` by another trader.

After strong Application confirmation/authorization:

```text
AssignedTrader = self
Ownership = Owned
```

Record history/notification for previous owner as required.

---

## 11. Contact Owner handoff

Only the current Contact Owner may hand off initially; this is Application authorization.

Domain transition effect:

```text
Case.ContactOwnerId = target
```

No lifecycle/quote-state change.

Append `ContactOwnerChanged`.

---

## 12. WorkingQuote transitions

`WorkingQuoteTransitions` returns a new `WorkingQuote` and increments `StateVersion`.

### ApplyCalculated

- expected version must match
- update Calculated payload
- active mode becomes Calculated
- update audit metadata

### SwitchMode

Calculated -> Manual:

- create/activate empty Manual values if no current manual edit state exists for this switch semantics
- retain Calculated payload

Manual -> Calculated:

- retain Manual payload
- reactivate existing Calculated state

### UpdateManual

Allowed only in Manual mode.

Updates Price / Final Simple Yield and audit metadata.

---

## 13. Memo transitions

`SalesMemoTransitions` and `TraderMemoTransitions`:

- validate expected `StateVersion`
- normalize memo text consistently
- return a new SalesMemo / TraderMemo
- increment version

Memo transitions do not affect RFQ lifecycle.

---

## 14. Return to old conditions

Never reactivate an old Revision.

Instead:

1. create a new Draft Revision with a newly allocated RevisionId
2. copy selected historical conditions
3. set `CopiedFromRevisionId`
4. set `QuoteSeedRevisionId` to relevant old Revision
5. Confirm through normal `AmendmentTransitions.Confirm`
6. create/seed a **new** WorkingQuote for the new Revision
7. Trader reconfirms a new quote

This preserves chronological history.


---

# 03. Use Cases and Authorization

## 1. Domain / Application boundary

`Rfq.Application` is the Use Case layer. Do not create a separate `Rfq.UseCases` project.

The primary distinction is:

```text
Domain transition/factory
= given business data and explicit values, what valid business state results?

Application use case
= how does the system obtain those values, authorize the actor, call Domain logic,
  persist outputs, record events, interact with external services, and commit?
```

### Boundary decision rule

Do **not** decide Domain vs Application by asking whether an operation touches one object or multiple objects.

Ask instead:

> If these business changes were exposed as independently callable operations, could a caller create an invalid or incoherent business state?

If yes, the coherent transformation belongs in Domain even when it consumes/returns several Domain objects.

Example: Quote Confirm coherently creates both the new RFQ quote state and the immutable ConfirmedQuote snapshot. Exposing those as unrelated public Domain operations would permit "RFQ says Quoted but no snapshot" or "snapshot exists but RFQ remains Requested".

By contrast, obtaining current user/time/Business Date, loading objects, invoking a Domain transition, appending an Event, and persisting all outputs atomically is Application orchestration. The intermediate Domain values can still be valid even though the **use case** must commit them together.

### Application owns

- repository/query access
- current-user lookup
- role/desk authorization and visibility policy
- Case/Revision/Quote ID allocation orchestration
- current instant / Business-Date resolution
- external calculation calls
- event recording
- transaction/unit-of-work orchestration
- retry/reconciliation policy at system boundaries

### Domain owns

- construction invariants
- business-state preconditions
- business-state transition rules
- coherent creation/transformation of related Domain outputs when splitting them would permit invalid business state

### Data and operations

The Domain deliberately follows a "data represents state; operations transform state" style.

Prefer:

```text
RfqLifecycleTransitions.CloseHit(...)
RfqOwnershipTransitions.TakeOver(...)
WorkingQuoteTransitions.SwitchMode(...)
```

over mutable entity APIs whose primary effect is to change internal fields in place.

This is not a ban on member methods. Factories, validation, derived properties, version checks, and natural value-object behavior may live on their owning types. The important rule is that public mutable entity methods must not become an escape hatch around explicit business transitions.

---

## 2. Application use cases

Public Application use cases are operation-specific. Do not expose a generic workflow executor as the business API.

### RFQ / Revision

- CreateDraft
- CreateFromExisting
- UpdateInitialDraft
- ConfirmInitialDraft
- DiscardInitialDraft
- SaveAmendment
- ConfirmAmendment
- DiscardAmendment
- BulkConfirmAmendments
- BulkDiscardAmendments

### Ownership / responsibility

- PickUpRfq
- ReleaseRfq
- AssignTrader
- TakeOverRfq
- ChangeContactOwner

### Working Quote / Confirmed Quote

- CalculateWorkingQuote
- ChangeWorkingQuoteMode
- UpdateManualWorkingQuote
- ConfirmQuote
- WithdrawQuote
- BulkConfirmQuotes
- BulkWithdrawQuotes

### Lifecycle

- PresentQuote
- UnpresentQuote
- CloseHitRfq
- CloseAwayRfq
- CorrectOutcomeToHit
- CorrectOutcomeToAway
- CancelRfq
- ReopenRfq
- ExpireQuote
- relevant operation-specific Bulk use cases

Public close/correction APIs are explicit. Do not expose a generic `CloseRfq(outcome)` or `CorrectOutcome(outcome)` selector.

### Memos

- UpdateSalesMemo
- UpdateTraderMemo
- UpdateMemoOperation where role-specific Post Process orchestration needs a common application path

### Post Process

- GetPostProcessWorklist
- CommitPostProcessChanges

Post Process commit is a bulk transport/use-case boundary with per-Case atomicity:

- all staged changes for one Case commit atomically
- different Cases are independent
- expected failure for one Case does not roll back successful Cases
- successful Cases are removed from FE pending state
- failed/skipped Cases remain pending
- after any normal commit response, Post Process queries reconcile with authoritative server state

Visibility is a replaceable Application policy boundary separate from edit authorization.

### Query / support

Examples include:

- GetActiveSalesRfqs
- GetActiveTraderRfqs
- SearchRfqs
- GetPostProcessWorklist
- GetEventsAfter
- ScratchPricer
- ResolveRfqCreationContext
- security/client/master queries
- Get/Save GridConfig
- typed current-user settings

Query-side readers may project directly from relational state; they do not need to rehydrate the full Domain graph.

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

## 11. Error taxonomy and boundaries

Expected operational failures use one semantic classification shared across Domain, Application, API, Bulk, and background-worker boundaries.

```text
RfqException
├─ ExpectedRfqException
│  └─ RfqErrorKind
│     ├─ Validation
│     ├─ InvalidState
│     ├─ VersionConflict
│     ├─ NotFound
│     ├─ Forbidden
│     └─ CalculationFailure
└─ RfqInvariantException
   └─ DomainInvariantException
```

Existing Domain exceptions participate in that hierarchy:

- `DomainValidationException` -> Validation
- `DomainRuleViolationException` -> InvalidState
- `StateVersionMismatchException` -> VersionConflict
- `DomainInvariantException` -> unexpected invariant

Application-level expected errors include:

- `RfqRequestValidationException`
- `RfqNotFoundException`
- `RfqForbiddenException`
- `CalculationFailureException`

BCL exception types are not the public expected-error contract. In particular, `InvalidOperationException`, `ArgumentException`, `KeyNotFoundException`, and `UnauthorizedAccessException` must not be globally interpreted as normal 4xx business failures.

HTTP mapping is policy at the API boundary:

- Validation -> 400
- InvalidState -> 409
- VersionConflict -> 409
- NotFound -> 404
- Forbidden -> 403
- CalculationFailure -> 422
- unexpected/unclassified/invariant failure -> 500 with generic detail

Expected ProblemDetails may expose the semantic message and stable code. Unexpected ProblemDetails must not expose the underlying exception message.

All API ProblemDetails include a trace/correlation ID.

Bulk continuation policy also depends on `RfqErrorKind`, not on arbitrary BCL exception classes. Unsupported or unexpected errors abort the bulk operation rather than being silently converted into per-item failures.

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

ASP.NET Core remains code-first and authoritative.

HTTP APIs are organized by business/use-case feature rather than large horizontal controller buckets. Requests, responses, and API mappers live beside the feature that owns them.

At the HTTP boundary:

- typed Domain/Application IDs become transport primitives
- Domain enums/unions become API-owned typed contracts
- API models do not expose Domain/Application types directly
- concrete/static mapping is preferred over speculative mapper interfaces

Do not introduce a generic JSON settings bag.

Current-user settings use typed endpoints/contracts, including:

```text
GET/PUT /api/me/settings/quote-expiry
GET/PUT /api/me/settings/default-quote-mode
GET/PUT /api/me/settings/theme
```

Semantics:

- Quote Expiry is a typed `None | After(duration)` policy
- Default Quote Mode is typed `Calculated | Manual`
- Theme is typed `Light | Dark`

Persistence may store enum-like values as strings internally, but the frontend must not depend on raw persistence strings or untyped JSON for these settings.

Grid configuration is intentionally different:

```text
GET/PUT /api/me/grid-configs/{screenId}/{configKey}
```

The backend treats grid configuration payload as opaque versioned JSON because layout shape is frontend-owned.

Do not expose the internal lifecycle class hierarchy directly merely because it exists in Domain.

---

## 14. Source organization

Source folders express **business/feature ownership first**.

Prefer feature-local organization such as:

```text
RfqDrafts/
WorkingQuotes/
RfqLifecycle/
PostProcess/
RfqSearch/
```

where the feature owns its controller/request/response/mapping or use-case types.

Avoid making framework artifact type the primary top-level organization merely because all files are controllers, requests, responses, mappers, or services.

Technical grouping remains appropriate for concepts that are genuinely cross-cutting, for example:

- `Abstractions/`
- `Authorization/`
- `Errors/`
- persistence infrastructure/configuration
- application-wide incidents/observability

Folder structure is an ownership/discoverability rule, not a namespace migration requirement. Stable layer namespaces may remain even when folders are feature-oriented.

---

## 15. Roles and overlap

Roles are not necessarily mutually exclusive.

Conceptually:

```text
Sales
Trader
Manager   // future policy
```

A user may hold multiple roles.

Manager is not automatically an RFQ owner. Future Manager overrides belong in centralized authorization rather than Domain lifecycle rules or scattered endpoint checks.


---

# 04. UI / UX

## 1. General principles

This is a high-frequency internal operational desktop tool. Operator throughput and clarity are product requirements, not cosmetic preferences.

- Sales, Trader, and Post Process are distinct workspaces with different operating goals.
- Grid interaction and inline editing are first-class workflows.
- Prefer information density over vertically expensive decorative chrome.
- Repeated operations should remain low-friction and keyboard-friendly where practical.
- Do not introduce modal confirmation for every action; confirmation strength should follow operational risk.
- Strongly destructive/ownership-changing actions may require stronger confirmation than ordinary reversible work.
- Do not silently replace actively edited or staged data when remote changes arrive.
- Separate "there are remote updates" from "apply/reconcile those updates".
- Bulk selection and result reporting must make partial success visible.
- Business-state color has semantic meaning and must remain readable in both Light and Dark themes; important state should not rely on color alone.
- Personal settings are persisted per current user.
- Grid layout persistence is explicit and separate from general Settings.
- Avoid adding generic panels, dashboards, or workflow chrome without a concrete desk use case.

Top-level routes are:

```text
/sales
/trader
/post-process
```

---

## 2. Sales workspace

Sales is an RFQ-entry and customer-contact workspace.

Primary structure:

```text
toolbar
+--------------------------------------+------------------+
| RFQ grid                             | Work pane        |
|                                      | RFQ / Bulk tabs  |
+--------------------------------------+------------------+
result/status surfaces as needed
```

The grid remains primary. The work pane shows the active RFQ/New form and a Bulk tab.

### New / Draft

Unsaved New is FE-local.

Save Draft requires resolved Client and Security and then persists a Case/Draft Revision.

Confirm may occur directly from unsaved New once full validation succeeds.

Creation-context lookup supplies:

- category
- default Assigned Trader
- standard settlement date

The backend still revalidates authoritative values on create/confirm.

### Selection

Use Excel-like row selection without checkbox-oriented UI.

- click -> active row
- Ctrl/Cmd -> additive/toggle multi-select
- Shift -> range selection
- active row and bulk selection are related but not identical concepts
- multi-selection must not automatically force the Work Pane away from the active RFQ

The Bulk tab remains explicit and shows selection count.

### Row actions

Frequently used lifecycle actions may appear as compact row actions/context actions.

Actions must preserve the same Application authorization/state rules as full-pane operations.

### Amendment UX

Inline and pane editing intentionally use different amendment-start interactions while sharing the same persisted Draft model.

Inline editing:

- editing completion with no effective value change does nothing
- editing completion with a real value change creates or updates the pending amendment Draft
- the changed value is persisted immediately

Pane editing:

- `Start Amendment` explicitly creates the pending Draft
- after that, field edits autosave on editing completion when the effective value changed
- a zero-difference Draft is allowed but cannot be confirmed
- Draft removal is explicit through Discard rather than automatic cleanup

Changed cells are visually marked.

Draft contents remain Sales-side until confirmed.

### Live / Paused refresh

Sales supports Live and Paused operating modes.

Unsaved New state remains FE-local and does not block Live refresh.

Persisted Draft editing protects only the short-lived field-edit/save interaction, not the entire lifetime of the Draft or pane.

Sales inline row actions are disabled in Live mode because a remote refresh may relocate rows between visual targeting and click, allowing a valid action to be sent to the wrong Case. Paused mode provides the stable row position required for those inline actions.

This inline-row restriction is an interaction-safety rule, not a general prohibition on Sales commands while Live.

### Recent revisions

A Recent Revisions surface may summarize recent RFQ/quote revisions for operational awareness. It is not the audit source of truth.

---

## 3. Trader workspace

Trader is a dense inline quoting workstation.

Primary structure:

```text
toolbar
+--------------------------------------------------+----------------+
| Active RFQ grid                                  | Operations /   |
|                                                  | Pricer pane    |
+--------------------------------------------------+----------------+
| RFQ Search / result grid                         |                |
+--------------------------------------------------+----------------+
```

The side pane may collapse; search remains available without turning the screen into a separate navigation flow.

### Selection

Use Excel-like row selection without visible selection checkboxes.

Selection supports bulk Pick/Confirm/operations while retaining a clear active row.

### Quote editing

Quote cells are edited inline.

The edited cell determines the calculation driver.

For each RFQ:

- at most one calculation request is considered current at a time
- stale/late responses must not overwrite newer intent
- calculation failure does not mutate WorkingQuote
- unrelated RFQs must remain operable while one Case is calculating

### Manual mode

Calculated -> Manual starts Manual values empty.

Calculated payload remains retained separately.

Manual -> Calculated restores retained calculated state.

### Quote confirmation

Confirm operates on eligible selected RFQs and uses optimistic versions.

Bulk confirmation is Case-by-Case with explicit result reporting.

### Ownership operations

Pick/Release/Assign/Take Over keep their existing confirmation semantics:

- self-assigned unowned Pick can be direct
- picking another Trader's assignment requires confirmation
- Take Over of another owner requires strong confirmation
- multi-select Pick follows the same safety semantics consistently

### Search

Past/search RFQ is server queried and displayed in a dedicated result grid.

Current pragmatic design keeps bounded result sets and client-side grid interaction rather than introducing complex paging infrastructure prematurely.

### Pricer

Pricer is scratch state independent from official WorkingQuote after load.

It may start empty or from an RFQ.

Apply-back to official WorkingQuote remains deferred unless explicitly implemented later.

### Live / Paused refresh

Trader supports Live / Paused plus explicit Refresh.

Trader operations whose target is already fixed by `CaseId` through selection/pane state remain available in Live mode; row relocation does not retarget those operations.

Short-lived local work that would be invalidated by replacement, such as active cell editing or an in-flight calculation/confirmation interaction, may temporarily protect refresh until that interaction finishes.

This is intentionally different from Sales inline row actions: the safety criterion is target stability, not screen identity.

---

## 4. Post Process workspace

Post Process replaces the earlier lightweight Daily Review/EOD placeholder.

Purpose:

- operational cleanup after/in addition to intraday quoting
- find unclosed RFQs
- close Hit/Away/Cancelled
- correct same-Business-Date Hit/Away outcomes
- update the current user's own-side memo
- review today's relevant RFQs

It is not another Sales entry screen or Trader quoting screen.

Quote values are context only.

### Worklist controls

Primary toolbar:

```text
[ Today | Unclosed ] [ Mine | All permitted ] [ Confirm Changes (N) ] [ Refresh ]
```

### Unclosed

Includes current:

- Active
- Presented

across Business Dates.

Excludes Draft, Cancelled, Hit, Away.

### Today

Today is defined by persisted Business Date facts, not timestamp ranges.

An RFQ belongs to Today when, for the current Business Date, it was:

- first opened/confirmed (`CreatedBusinessDate`)
- closed Hit/Away
- Cancelled
- outcome-corrected

Quote/memo activity alone does not pull an older RFQ into Today.

### Scope

`Mine` means the current user matches at least one of:

- SalesId
- ContactOwnerId
- AssignedTraderId

`All permitted` is determined through a replaceable visibility policy.

Visibility does not grant edit authority.

### Staging and commit

Post Process changes are FE-local until Confirm Changes.

Pending state is keyed by CaseId and survives:

- Today <-> Unclosed
- Mine <-> All permitted

Per Case, lifecycle and own-side memo changes are staged and reviewed together.

Commit semantics:

- one Case is atomic
- different Cases are independent
- successful Cases leave pending state
- failed/skipped Cases remain pending
- any normal commit response invalidates/reconciles Post Process query caches with authoritative server state

Refresh discards pending changes only after confirmation.

Browser reload/close and SPA navigation away from Post Process warn when pending changes exist.

### Outcome correction

Only Hit <-> Away correction is supported.

Rules:

- current Business Date must equal original `ClosedBusinessDate`
- a new non-empty reason is required
- historical correction reason may be displayed read-only when no new correction is staged
- a newly staged correction reason starts empty and never implicitly reuses the previous audit reason

Reopen is not a Post Process action.

### Memo

Post Process exposes only the current user's own-side memo:

- Sales role -> Sales Memo
- Trader role -> Trader Memo

Do not expose/edit the opposite side's memo.

---

## 5. Refresh and remote-change semantics

Persisted Events + SSE provide remote-change awareness.

SSE is a wake-up mechanism, not authoritative UI data. Normal page queries/persisted projections remain the source of truth.

### State ownership

Sales/Trader frontend state is kept in three distinct categories:

1. **Authoritative query state** — the latest server projection.
2. **Paused display snapshot** — a separate snapshot that exists only to preserve Paused-mode display.
3. **Local interaction state** — unsaved New input, an active field/cell editor, dialogs, and similar UI-local intent.

Do not maintain a second mutable copy of Live server state merely to drive the grid. Do not derive lifecycle/ownership/quote transitions in the frontend after a successful mutation.

### Live

Live follows authoritative server state.

On a remote wake-up:

- when no protected interaction is active, perform authoritative catch-up
- when a protected interaction is active, do not replace that interaction; mark that an update is pending and perform one authoritative catch-up when protection ends

Protection is deliberately short-lived. It covers the field/cell editing and save/in-flight interval that would be unsafe to replace. It is not a long-lived page mode, pane-open flag, Draft-lifetime flag, or unsaved-New flag.

### Paused

Paused preserves its display snapshot when remote changes arrive.

Remote changes set a boolean **Updates pending** indication. This is not a count of changed Cases or received events.

Explicit Refresh replaces the Paused snapshot with authoritative state while remaining Paused.

Paused -> Live performs authoritative catch-up, drops the Paused snapshot, and resumes Live behavior.

Explicit Refresh and Paused -> Live are disabled while a protected interaction is active. Once the short-lived interaction finishes, they become available again.

Successful explicit Refresh does not require a success toast; the refreshed data and cleared pending indication are the normal feedback. Refresh failure is surfaced explicitly.

### Reconciliation after the current user's mutations

Mutation responses are not used to predict the resulting business state in the frontend.

- in Live, successful mutations are followed by authoritative page catch-up
- in Paused, successful single-Case mutations re-read that Case through the page-specific authoritative query/projection and replace only that Case in the Paused snapshot
- in Paused bulk operations, only Succeeded Cases are authoritatively re-read and replaced; Failed/Skipped Cases and unrelated rows remain on the Paused snapshot

Page-specific catch-up side effects may differ, for example Sales recent-revision refresh or Trader calculation invalidation, without changing these core semantics.

Post Process remains separate: it uses explicit refresh plus mutation-triggered authoritative query reconciliation and does not need Sales/Trader Live/Pause behavior.

---

## 6. Result reporting

Bulk/multi-item actions report per-Case results.

Do not hide partial success.

UI should distinguish:

- Succeeded
- Skipped
- Failed

Results may use compact expandable bars rather than modal-only reporting.

---

## 7. Personal Settings

Use a small Settings surface rather than scattering persistent user preferences through toolbars.

Current typed settings:

### Theme

```text
Light
Dark
```

Theme switching is functional, not persistence-only.

One application-level theme state drives:

- Ant Design
- AG Grid
- custom application CSS
- portals such as Drawer/Modal/context menus

Do not implement separate inconsistent theme toggles per component library.

### Quote Expiry

Trader preference for the next Working Quote/quote-confirm flow.

The current UI offers:

- None
- 15 minutes
- 1 hour

The underlying contract remains typed `None | After(duration)`, not a magic nullable integer in the frontend.

### Default Quote Mode

Typed:

```text
Calculated
Manual
```

Changing the preference affects subsequent/new Working Quote usage as designed; it does not retroactively rewrite existing business state.

### Save semantics

Settings are edited in a local form state and persisted explicitly on Save.

Do not create a generic arbitrary JSON settings bag.

---

## 8. Theme and color semantics

Custom application colors use semantic tokens rather than literal color-name tokens.

Examples of business semantics that remain distinct:

- Sales quoted row
- Sales draft/pending-amendment row
- Trader high-attention row
- Trader work-attention row
- Post Process unclosed row
- terminal/cancelled muted foreground/background
- selected-row indicator
- pending-change indicator
- amendment-changed cell background/indicator
- generic surfaces, borders, secondary text
- success/warning/error states

Two UI meanings must not share one token merely because their current RGB happens to match.

In particular:

- selected-row left marker
- Post Process pending-change left marker

are separate semantic tokens.

Ant Design semantic states such as success/error/warning/processing/danger should remain semantic and follow the selected theme rather than being replaced by hard-coded green/red/orange values.

The current Dark visual appearance is the baseline to preserve while adding a proper Light equivalent.

---

## 9. Grid configuration

Grid configuration is user-specific and persisted server-side.

Logical key:

```text
(UserId, ScreenId, ConfigKey)
```

Persist layout-oriented state such as:

- column order
- width
- visibility
- pinning
- sort where intentionally included

Do not persist transient interaction state such as:

- selection
- scroll position
- active editor
- pending business edits

Filter persistence should be deliberate rather than accidental.

Grid layout remains frontend-owned opaque JSON at the backend boundary.

The current frontend payload contains AG Grid column state plus column-group state. The backend does not interpret those fields.

### Interaction

Do not consume a dedicated vertical toolbar row merely for layout controls.

Expose compact Grid context-menu actions:

```text
Save Layout
Load Layout
Reset Layout
```

or equivalent concise wording.

Save is explicit; do not autosave layout.

Current interaction semantics:

- Save captures the current column and column-group layout and persists it.
- Load reapplies the last persisted layout.
- Reset restores the code/default layout locally.
- Reset does not implicitly overwrite the persisted saved layout; Save after Reset if the default should become the new saved layout.

Independent grids use independent config keys. Sales main, Trader main/search/confirm, and Post Process main layouts must not accidentally share one layout payload.

---

# 05. Persistence and Events

## 1. Persistence principles

- Persistence shape may differ from Domain type shape.
- Domain types do not expose DB implementation details.
- Current operational state is stored directly; this is not event sourcing.
- Events provide audit/history and notification feed, not the sole state-rebuild source.
- Confirmed business snapshots are immutable.
- Flattened persistence status columns are projections of the typed Domain state, not a second Domain source of truth.

---

## 2. Logical tables

### `RfqCase`

Primary key: `CaseId`.

Contains stable Case facts such as:

- ClientId
- SecurityId
- SalesId?
- CategorySnapshot (`CategoryId`)
- CreatedAt
- CreatedBusinessDate? // null only while initial Case remains Draft
- CreatedBy
- CopiedFromCaseId?
- source metadata if added later

### `CaseCurrent`

Primary key: `CaseId`, 1:1 with `RfqCase`.

A flattened current projection may contain:

- lifecycle kind / RFQ status projection
- quote status projection / request reason projection
- CurrentRevisionId
- CurrentQuoteId?
- ClosedQuoteId?
- ClosedBusinessDate?
- ContactOwnerId
- AssignedTraderId
- ownership boolean projection
- Version (`bigint`)

This flattened shape is acceptable even though Domain uses `ActiveRfq`, `PresentedRfq`, `QuoteRequested`, `QuoteConfirmed`, and typed `Ownership`.

Mapper logic must derive these columns from Domain state and reconstruct only valid Domain state on load.

`CurrentQuoteId` is operational. `ClosedQuoteId` records the immutable ConfirmedQuote used to resolve a closed Hit/Away case.

### `RfqRevision`

Primary key: `RevisionId`, FK to Case.

Contains:

- Status
- Revision terms
- CopiedFromRevisionId?
- QuoteSeedRevisionId?
- Version
- created/confirmed metadata

### `WorkingQuote`

Primary key may be `RevisionId` to enforce one-per-Revision.

Contains:

- RevisionId
- active mode
- calculated payload
- manual payload
- version
- audit/update metadata

The persistence row may be updated in place even though the Domain object is immutable; repository mapping applies the returned new Domain value to the tracked EF entity.

### `ConfirmedQuote`

Primary key: `QuoteId`, FK to `RevisionId`.

Immutable snapshot.

No mutable Presented/Withdrawn/Expired flags belong on it.

Current supported expiry persistence may remain:

- expiry minutes / null
- resolved ExpiresAt / null

while Domain uses a typed expiry policy.

### `SalesMemo` / `TraderMemo`

Separate 1:1 rows per Case.

Each stores:

- CaseId
- Value
- Version

The two rows reflect the separate Domain objects and own-side semantics. They are not one generic CaseMemo row.

### `Category`

Master data:

```text
CategoryId   // stable key
Name         // mutable display name
```

`Security.CategoryId` and `CategoryRouting.CategoryId` reference this master through FKs.

### Other existing tables

Retain current logical roles for:

- `CategoryRouting`
- `CalculationFailureLog`
- `UserGridConfig`
- typed current-user preference storage (quote expiry, default quote mode, theme)
- event cursor / Event tables

---

## 3. Business Date persistence

Business Date is stored as a date fact where operational semantics depend on desk day.

Required persisted facts include:

- Case `CreatedBusinessDate`
- closed-state `ClosedBusinessDate`
- event `BusinessDate` for RFQ events used by Today/Post Process semantics

These values are written by Application use cases using the authoritative business-date provider.

Do not implement Today/Post Process semantics by converting timestamps in SQL or by assuming UTC date equals desk date.

Outcome correction preserves the original `ClosedBusinessDate`; the correction event carries the current Business Date.

---

## 4. StateVersion mapping

Domain/Application use `StateVersion`.

DB remains signed `bigint`/`long` and EF concurrency token.

Map at the Infrastructure boundary.

Do not change DB columns to unsigned types.

---

## 5. Domain rehydration

Persistence reconstruction must not require public mutable setters or public arbitrary `Restore` escape hatches.

Use non-public constructors / `internal Restore` or equivalent and narrowly allow Infrastructure access, e.g. `InternalsVisibleTo("Rfq.Infrastructure")`.

Application code should use public factories/transitions, not rehydration APIs.

If persisted columns represent an impossible combination, mapping should fail as a Domain invariant/data-integrity problem rather than silently constructing invalid state.

---

## 6. Event persistence

Use a thin shared parent **at DB level**.

### `Event`

```text
EventId
OccurredAt
ActorUserId?
```

`EventId` is global for event retrieval/cursor purposes.

The cursor must be **commit-order safe**. Plain identity/sequence allocation before commit is not sufficient by itself.

A DB-serialized cursor allocator held through transaction commit is acceptable. Whatever mechanism is used must be proven by a real PostgreSQL concurrency test that no late-committing event can be permanently skipped.

### `RfqEvent`

```text
EventId -> Event
CaseId
Type
BusinessDate?
Payload jsonb
```

`BusinessDate` belongs on the RFQ-event child row because it is an RFQ business-day fact used by Post Process/operational queries. Quote events do not acquire a Business Date merely because they share the parent Event row.

### `QuoteEvent`

```text
EventId -> Event
QuoteId
Type
Payload jsonb
```

**Do not store `CaseId` on `QuoteEvent`.**

Case identity is derived through:

```text
QuoteEvent.QuoteId
-> ConfirmedQuote.RevisionId
-> RfqRevision.CaseId
```

This prevents the DB from permitting a QuoteEvent whose CaseId and QuoteId refer to different Cases.

Queries that need CaseId join through the relation.

---

## 7. Event types

### RFQ event candidates

- RevisionConfirmed
- Cancelled
- Reopened
- ClosedHit
- ClosedAway
- OutcomeCorrected
- ContactOwnerChanged
- TakenOver
- PickedUp / Released / AssignedTraderChanged where useful

### Quote event types

- Confirmed
- Presented
- Unpresented
- Withdrawn
- Expired

Quote events refer to immutable ConfirmedQuote by `QuoteId`.

Quote Confirm emits `Confirmed` so other sessions can discover Requested -> Quoted through the persisted feed.

State mutation and event append occur in the same use-case transaction.

---

## 8. Authoritative data vs projection

Authoritative business data includes:

- RfqCase / lifecycle state as persisted through tables
- RfqRevision
- WorkingQuote
- ConfirmedQuote
- SalesMemo / TraderMemo
- Event / RfqEvent / QuoteEvent
- configuration/master data
- calculation failure log

`CaseCurrent` is the transactionally maintained current operational projection/flattened persistence slice.

Do not rebuild it from events on every request.

---

## 9. Repository boundaries

Application-facing repositories remain business/domain-oriented.

Avoid exposing column operations such as:

```text
SetCurrentQuoteId(null)
SetStatusColumn(...)
InsertQuoteEventRow(...)
```

Application invokes Domain transitions/factories and asks repositories to persist the resulting typed values.

Query-side repositories/readers may project directly into query DTOs.

---

## 10. Unit of Work / transactions

A scoped EF `DbContext` may back multiple repositories.

Application sees `IUnitOfWork` or equivalent.

General rule:

```text
one business use case
-> all related persistence updates/events
-> one atomic commit
```

Examples include:

- Quote Confirm: CaseCurrent + ConfirmedQuote + QuoteEvent
- Initial Confirm: Case/Revision current state + WorkingQuote + event(s)
- Amendment Confirm: revisions + CaseCurrent + new WorkingQuote + event(s)
- Expire: CaseCurrent + QuoteEvent

Do not hold DB locks while performing external/heavy calculation.

---

### Bulk transaction semantics

A bulk HTTP request is not one atomic business transaction.

For operation-specific bulk use cases:

- execute the corresponding single-item use case per Case
- one Case succeeds/fails atomically
- successful prior Cases remain committed if a later Case fails with a recoverable expected error
- expected recoverable failure must discard/reset the current scoped EF changes before continuing
- unexpected/unclassified errors abort rather than being silently converted into item failures

Do not duplicate single-item transition logic inside bulk implementations.

---

## 11. Loading strategy

Command-side retrieval loads only state needed for the transition:

- Case facts/current lifecycle
- Current Revision
- pending Draft if relevant
- WorkingQuote/current ConfirmedQuote/seed WorkingQuote as relevant

Do not routinely load all historical Revisions, all quotes, or all Events.

History/search uses query-side DTOs.

---

## 12. DB constraints

Minimum constraints include:

- one CaseCurrent per Case
- at most one Draft Revision per Case
- at most one WorkingQuote per Revision
- valid CurrentRevision FK
- valid CurrentQuote FK when present
- valid ClosedQuote FK when present
- Security -> Category FK
- CategoryRouting -> Category FK
- optimistic concurrency version columns

PostgreSQL partial unique index remains appropriate for one Draft per Case.

Domain rules and DB constraints both protect critical invariants.

---

## 13. Indexing and search projection

Keep current pragmatic indexing strategy for expiry worker and Past RFQ search.

Do not create broad speculative compound indexes or a full copied search model until usage requires it.

A future thin search projection may store references, but do not duplicate every display field prematurely.

---

## 14. Initial indexing

Initial indexes should cover actual operational queries, including:

- expiry worker: current confirmed/quoted items + `ExpiresAt`
- Past RFQ search by date/client/security/category/contact owner/assigned trader/status
- stable PK/FK joins

Do not pre-create every possible compound index. Observe real search patterns and add targeted indexes.

---

## 15. Past RFQ read model

Do not build a large copied snapshot before evidence requires it.

Initial approach:

- ordinary relational joins
- direct query DTO
- indexes
- optionally a very thin reference projection if proven useful

Do not duplicate every display field prematurely or default to materialized views with refresh-management complexity.


---

# 06. Calculation and Search

## 1. Calculation service boundary

The RFQ application does not own detailed pricing conventions, curve resolution, security convention logic, or standard-settlement calculation.

The Calculation boundary owns pricing/calculation semantics. The RFQ application owns workflow semantics.

### RFQ Application remains authoritative for

- RFQ lifecycle
- Revision/amendment workflow
- Contact Owner / Assigned Trader / trader ownership
- WorkingQuote persistence and versioning
- ConfirmedQuote lifecycle relationship
- actor authorization
- RFQ-level optimistic concurrency
- business transaction/event orchestration

### Calculation service/client owns

- price/yield/spread calculations
- security pricing/convention interpretation
- curve/reference-data-dependent calculation
- standard-settlement resolution
- calculation-specific error/result semantics

Calculation receives typed business/application inputs per item, conceptually:

- SecurityId
- SettlementDate
- CalculationDriver
- typed CalculationParameter
- independent SimpleYieldSlide where applicable
- correlation/request ID

and returns one result per request.

```text
CalculateBulk(requests[]) -> results[]
```

Do not rely on array ordering alone.

External transport may map typed IDs to strings/Guids at the adapter boundary.

The Calculation service must not become a hidden RFQ aggregate or persistent workflow owner merely because it is invoked during quote editing.

---

## 2. Bulk result semantics

One failed calculation does not fail the entire batch.

```text
CalculationResult =
    Success { payload }
  | Error { code, message }
```

---

## 3. Initial mock calculation client

Keep `ICalculationClient` with mock implementation.

Mock requirements:

- deterministic
- arbitrary Security IDs work
- plausible-looking values
- typed requests/results
- controllable per-item failure
- same broad bulk shape expected from future real service

Pricing accuracy is not the purpose of this application.

---

## 4. WorkingQuote update flow

Trader edits a quote cell.

1. load typed edit context including CaseId, RevisionId, SecurityId, ownership/state, `StateVersion`, and WorkingQuote
2. authorize actor centrally
3. verify expected Case/WorkingQuote versions
4. build typed calculation request
5. call calculation outside DB transaction
6. on Error:
   - do not change WorkingQuote
   - persist CalculationFailureLog
   - return failure so FE reverts attempted edit
7. on Success:
   - reload current RFQ state
   - revalidate current Revision, ownership/authorization, Case Version, WorkingQuote Version
   - call `WorkingQuoteTransitions.ApplyCalculated`
   - persist returned WorkingQuote

Do not compare statuses as strings such as `"Requested"`.

Do not hold DB locks during external/heavy calculation.

---

## 5. WorkingQuote factory

Creation is separate from update transitions.

Use a Domain factory that creates a WorkingQuote only for an eligible confirmed/current Revision.

Initial Confirm:

```text
RfqLifecycleTransitions.ConfirmInitial
-> WorkingQuoteFactory.CreateInitialFor(...)
-> persist atomically
```

Amendment Confirm:

- create a new WorkingQuote for the new current Revision
- supply seed WorkingQuote loaded by Application/Infrastructure when `QuoteSeedRevisionId` requires it
- Domain factory decides empty vs clone semantics from explicit inputs

Database uniqueness remains the final one-per-Revision guard.

---

## 6. Calculation failure log

Keep append-only failure logging with enough context to reproduce the attempted request:

- FailureLogId
- CaseId
- RevisionId
- TraderId
- RequestId
- driver/type/value
- slide
- prior WorkingQuote snapshot
- request/calculation context
- error code/message
- timestamp

Use typed Domain/Application IDs before persistence mapping.

---

## 7. Calculation context

Calculated ConfirmedQuotes preserve enough context for historical reproducibility, such as:

- market date/as-of
- snapshot tag
- reference securities/yields
- curve/context identifiers
- method-specific inputs

Use typed family-specific payloads; avoid a giant nullable field forest.

---

## 8. Standard settlement

Resolved by calculation/library boundary, not reimplemented in RFQ Domain.

```text
ResolveStandardSettlementDate(SecurityId, TradeDate)
    -> SettlementDate
```

On security change before confirm, recompute standard settlement.

Store standard and actual settlement in Revision terms.

Business date is resolved through the configured desk/business timezone abstraction, not server-local or UTC calendar date shortcuts.

---

## 9. Security and Category resolution

Security search belongs to the RFQ App Server boundary.

Conceptual abstraction:

```text
ISecuritySearch
- Search(query)
- Resolve(SecurityId)
```

Save canonical typed `SecurityId` in Application/Domain.

Security -> Category is master/DB data, not a hard-coded Domain enum rule.

The consolidated defaults resolver may coordinate:

```text
SecurityId
-> CategoryId
-> Default Assigned Trader
-> Standard Settlement Date
```

---

## 10. Security search behavior

Retain current search behavior and normalization strategies for:

- internal code
- BBG-like display/search
- ISIN prefix/full lookup

Union/deduplicate/rank results; exact/normalized exact matches rank above broad partials.

Do not hardcode JP-only numeric ISIN assumptions.

---

## 11. Client search

Simple autocomplete/partial search over available name/code fields.

Return canonical `ClientId`.

---

## 12. Search caching

Initial behavior remains direct PostgreSQL search/exact lookup.

Do not preload the full security master or aggressively cache arbitrary autocomplete queries.

If later needed, exact ID lookup is the first reasonable cache target.

---

## 13. Security search details

Retain the current canonical search rules.

Search strategies may run together; do not force every query through one exclusive parser branch. Union results, deduplicate, then rank. Exact/normalized exact/structured matches rank above prefix/partial matches.

### Internal code

Short form:

```text
{int}-{int}
```

Normalize conceptually to:

```text
0-02-XXXX-YYYYY
```

by trimming components and zero-padding latter fields.

Full form:

```text
{int}-{int}-{int}-{int}
```

Normalize widths approximately `[1, 2, 4, 5]` and support prefix/partial search.

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
- ticker + coupon may search without maturity

### ISIN

After trim + uppercase, support prefix candidates.

Useful candidate pattern:

```text
^[A-Z]{2}[A-Z0-9]{5,10}$
```

- 7–11 characters -> prefix search
- 12 characters -> validate structure/checksum if desired

Do not hardcode JP-only numeric NSIN assumptions.

### Result display

Useful candidate columns:

- Japanese security name
- BBG-style display
- Internal Code
- ISIN

Issuer is optional if reliable issuer master data exists.

Limit results to top N and ask for more input when matches are broad.

---

## 14. RFQ defaults resolver

The FE may use a consolidated query after Security selection:

```text
ResolveRfqDefaults(SecurityId, TradeDate)
```

It may return:

- CategoryId / category display data
- Default Assigned Trader
- Standard Settlement Date

This keeps FE simple while Application coordinates master/routing/calculation lookups.


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

For QuoteEvent, Case context is derived by joining `QuoteId -> ConfirmedQuote -> Revision -> Case`; QuoteEvent itself does not redundantly store CaseId.

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

and runs the normal `ExpireQuote` application use case, which invokes the Domain `QuoteTransitions.Expire` transition.

Do not implement expiry as ad-hoc SQL state mutation disconnected from Domain/Application rules.

---

## 7. Expiry idempotency and concurrency

Initial deployment assumes one App Server, but ExpireQuote should be safe if attempted more than once.

Use typed state and `StateVersion` preconditions so only a still-current confirmed quote can transition.

If a human action already Withdrawn/Closed/Revised the RFQ, expiry should do nothing or return a harmless no-op/conflict outcome according to the use-case contract.

Future multi-instance coordination is deferred.

---

## 8. Logging, audit, observability

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

Business-state races that the use case already classifies as expected must not be reintroduced as broad swallowed `InvalidOperationException` catches.

### Tracing / metrics

Use standard .NET primitives such as `ActivitySource` and `Meter`.

---

## 9. Event retention

Initial implementation does not delete Events by count or age.

Retain events; archive/partition later only if actual growth/compliance requires it.

---

## 10. Important event filtering

Do not toast every Desk event.

Server uses current-user/screen scope to decide which events are important; normal events can still set Updates Available.

---

## 11. Authentication scope

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
- bulk partial-success behavior
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

### Required event-cursor concurrency test

Prove the global event feed cannot lose an event when concurrent event-producing transactions are ordered/committed oppositely.

The test must exercise real PostgreSQL and the actual cursor-lock/allocation mechanism.

The invariant to prove is:

```text
once a client advances lastSeenEventId after committed events,
no event that later becomes visible may exist at a skipped lower cursor value
```

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

Existing FE test scope remains unchanged; backend semantic refactor should not require FE changes.

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

# 09. Scope, Non-goals, and Deferred Work

## 1. Explicitly out of initial/current scope

### Booking / ticket workflow

Hit/Away are RFQ workflow outcomes.

A Hit may later trigger or feed booking, but Booking is a separate downstream workflow/integration boundary. Do not make `CloseHitRfq` itself the owner of a booking aggregate, booking lifecycle, or booking reconciliation state.

### External inbound channels

No Bloomberg Chat/direct inbound adapter initially.

### New Bulk / List / Thread / Portfolio workflow

Do not design generic grouping before desk workflow is known.

### Real calculation server

Current integration uses MockCalculationClient; preserve replaceable typed boundary.

### Realtime market feed

No realtime market-data integration initially.

### Full authentication architecture

Current user/roles are supplied by hosting environment; transport is not specified here.

### Multi-instance App Server

Single instance initially; no Redis/distributed fan-out/leader election.

### Full event sourcing

Events are audit/notification/history. Current state is stored directly.

### Sophisticated Past RFQ paging / large materialized search model

Keep the initial pragmatic query/index/cap design until real usage demands more.

### Desktop/browser notifications

In-app notifications only initially.

### Manager workflow

Authorization is centralized so richer Manager overrides can be added later; policy is deferred.

Post Process `All permitted` visibility is intentionally behind a replaceable policy seam and does not itself define a complete Manager role model.

### Pricer apply-back

Pricer remains independent scratch state initially.

### Dynamic category administration UI

Category is master data and routing is configurable in DB, but a new admin UI/workflow is not part of the current scope.

---

## 2. Deliberately simplified current assumptions

- Side = Customer Sell
- Security Type = Bond
- Contact Owner exists
- Assigned Trader exists by Confirm
- one Draft Revision maximum per Case
- one WorkingQuote maximum per Revision
- quote expiry supports None or a positive fixed duration
- expiry check interval ≈ 10 seconds
- no auto-Away at Post Process / end-of-day
- no silent replacement of protected/actively edited Sales/Trader grid state
- security/client search hits DB directly without broad result caching
- Category values come from master data rather than a compile-time enum

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
- browser/desktop notifications
- paging / large-history optimization
- dedicated search projection
- event archival/partitioning
- richer expiry policies such as fixed time / AM / PM / EOD
- complete calculation attempt audit
- pricer -> WorkingQuote apply-back
- external manager/master services
- richer category/security mapping rules

---

## 4. Known implementation-sensitive areas

Keep adaptable:

- exact EF Core flattened mapping of typed lifecycle states
- exact serialization schema for WorkingQuote/CalculationContext payloads
- endpoint/DTO naming
- SSE behavior through real proxy/LB
- existing-user auth integration
- security-search index strategy
- grid keyboard shortcuts
- Sales lower-panel details
- exact future booking/reconciliation workflow after Post Process
- manager override policy

The design intentionally fixes business invariants while leaving these implementation details replaceable.


---

# 10. Design Decisions and Rationale

This document records non-obvious choices that should survive implementation handoff.

---

## 1. Domain state is immutable from callers

Business-state objects such as `RfqCase`, `RfqRevision`, `WorkingQuote`, and `CaseMemo` do not expose public mutation methods/setters.

State changes return new values through Domain transitions.

Rationale:

- makes transitions explicit and reviewable
- prevents Application code from bypassing business preconditions by setting fields directly
- reduces accidental partially-updated state
- matches the desired “data represents state; processing is outside the data” model

This does not require every type to be a record.

---

## 2. Typed lifecycle replaces field-combination source of truth

The earlier design kept Open state primarily as fields (`RfqStatus`, `QuoteStatus`, reason, current quote ID). The revised design uses:

```text
RfqLifecycle
├─ DraftRfq
├─ OpenRfq
│  ├─ ActiveRfq
│  │  ├─ QuoteRequested
│  │  └─ QuoteConfirmed
│  └─ PresentedRfq
├─ CancelledRfq
└─ ClosedRfq
   ├─ HitRfq
   └─ AwayRfq
```

Rationale:

- Presented always requires a quote and deserves its own RFQ state type
- Requested always requires a reason
- Confirmed/Quoted always requires a QuoteId
- impossible nullable/status combinations become unrepresentable through public construction

`RfqStatus` and `QuoteStatus` remain useful projection/transport concepts but are not a second mutable Domain truth.

---

## 3. Presented is an Open RFQ subtype

`PresentedRfq` is under `OpenRfq`, not a top-level lifecycle beside Open.

Rationale:

- the RFQ is still operational/open
- it retains Open-only concepts such as trader ownership
- Present/Unpresent changes customer-facing exposure while keeping the RFQ in the open workflow

---

## 4. Contact Owner and Assigned Trader are Case-level

They are not duplicated in every lifecycle subtype.

Rationale:

- they survive Cancel and Close
- they are Case responsibility/routing data rather than a property of Active/Presented alone
- duplication created synchronization risk

---

## 5. Trader ownership is typed and Open-only

Use `Ownership = Unowned | Owned` in `OpenRfq` rather than a Domain boolean `Owned`.

Rationale:

- `Owned` by itself was hard to read in UI/code context
- Domain can use explicit `Ownership` while UI displays “Picked Up”
- ownership has no operational meaning after leaving Open
- `Owned` still means owner == current `AssignedTraderId`; no second owner ID

Persistence may flatten to boolean.

---

## 6. Transitions are grouped by business meaning, not object count

Canonical examples:

```text
RfqLifecycleTransitions
RfqOwnershipTransitions
QuoteTransitions
AmendmentTransitions
WorkingQuoteTransitions
CaseMemoTransitions
```

Rationale:

- classification must remain stable if implementation later updates another related object
- “one state machine vs multiple objects” is an implementation-sensitive boundary and produced awkward categorization
- business vocabulary gives a more stable API

A transition may consume/return several Domain values.

---

## 7. Application is the Use Case layer

Do not add a separate UseCases project.

Application owns repository access, authorization, ID/time resolution, external services, event persistence, and transaction orchestration.

Domain transitions/factories own deterministic business-state rules.

Rationale:

- `Rfq.Application` already represents this layer
- splitting `Application` and `UseCases` would create an unclear project boundary with little dependency benefit
- Application size is managed by feature/use-case folders, not another assembly

---

## 8. Authorization and state validity are separate concerns

Domain checks whether the transition is valid for the current state.

Application authorization checks whether the current actor may perform it.

Rationale:

- role/desk/manager policy will evolve
- state invariants should not depend on current-user infrastructure
- central authorization avoids repeated predicates across handlers/controllers

---

## 9. Quote Confirm creates RFQ state + ConfirmedQuote coherently

Public Domain APIs must not allow Application to independently create a current ConfirmedQuote and separately mark the RFQ quoted.

Quote Confirm produces both:

```text
ActiveRfq(QuoteRequested)
+ WorkingQuote
->
ActiveRfq(QuoteConfirmed(new QuoteId))
+ immutable ConfirmedQuote
```

Rationale:

- otherwise Application can construct a ConfirmedQuote while RFQ stays Requested, or mark RFQ quoted without its corresponding snapshot
- this is a Domain invariant, not merely persistence orchestration

---

## 10. Quote identity allocation is outside the transition

Application allocates `QuoteId` and `RevisionId` and passes them into Domain APIs.

CaseId follows the existing DB/application sequence path.

Rationale:

- ID value generation is not a business transition rule
- deterministic transitions are easier to test
- local Guid creation does not justify an interface without a concrete replaceability requirement

---

## 11. QuoteConfirmation is a Domain concept

`ConfirmedBy + ConfirmedAt + expiry policy` belong together as confirmation metadata.

`QuoteId` remains separate because it is the identity of the new ConfirmedQuote.

Rationale:

- group values because they form a real concept, not because a method has “too many arguments”
- parameter count alone is not a reason to introduce wrapper objects

---

## 12. Expiry is typed, but future policies are not implemented early

Current Domain supports no expiry or positive fixed duration through a typed policy rather than raw `int?` minutes.

Rationale:

- future policies may not be expressible as N minutes
- raw nullable integer conflates absence, units, and policy
- current DB representation may remain minutes + resolved timestamp until requirements expand

---

## 13. WorkingQuote belongs to Revision and is immutable Domain data

A WorkingQuote is still conceptually one-per-Revision working state, but updates return a new WorkingQuote through `WorkingQuoteTransitions`.

Rationale:

- quote work is meaningful against a specific condition set
- historical revisions keep their own working values
- immutable Domain values make update/version semantics explicit

EF persistence may update the corresponding row in place.

---

## 14. WorkingQuote creation is a Domain factory

Initial Confirm does not need to make WorkingQuote creation part of the lifecycle transition itself.

Instead Application calls:

```text
ConfirmInitial transition
then WorkingQuote factory
then one atomic persistence commit
```

Rationale:

- “Open/Requested” is a coherent Domain state even before persistence orchestration creates its working object
- the final persisted use case still guarantees a WorkingQuote for the confirmed/current revision
- a Domain factory can validate which confirmed/current revision it is creating for without making repository queries

Amendment Confirm similarly creates a **new** WorkingQuote for the new Revision in the same Application transaction and may seed it explicitly.

---

## 15. CurrentRevision must be named for what it means

A property originally named `InitialRevision` must not later hold an amendment revision.

Use `CurrentRevision`.

Rationale:

- semantic naming matters more than historical creation order
- retaining the old name invites incorrect future logic

Transition results should carry affected old/new revisions when Application needs them; do not keep temporary mutable `PreviousRevision`/`DiscardedRevision` fields on the Case.

---

## 16. Revision is immutable and public low-level mutation is not exposed

Do not expose generic public `revision.Confirm()/Supersede()/Discard()` calls that Application can compose into an invalid Case.

Use business transitions (`InitialDraft`, `Amendment`, lifecycle confirm) and internal helpers as needed.

Rationale:

- Revision state is coupled to Case current-revision semantics
- low-level public operations would let callers violate Case invariants

---

## 17. StateVersion is a Domain value object over signed long

Use one common `StateVersion` for Case current state, Revision, WorkingQuote, and SalesMemo / TraderMemo.

Requirements:

- signed long
- >= 1
- checked `Next()`

Rationale:

- PostgreSQL/EF/JSON naturally use signed long
- `ulong` creates boundary friction with little benefit
- one VO removes naked-version primitives without creating unnecessary per-entity version types

---

## 18. Category is master data, not enum

Category uses stable `CategoryId` plus mutable display `Name`.

Security and routing reference the ID through DB constraints.

Rationale:

- categories may change rarely but can legitimately be data-driven
- display-name changes must not break references/history
- security-to-category mapping is not currently a fixed code rule
- UI can avoid free-text mistakes by selecting from the master

---

## 19. Typed IDs/values are used inside Domain and Application

Application internals should not carry domain semantics as raw strings/Guids/longs where a Domain type exists.

Rationale:

- prevents accidental string status comparisons
- makes contexts/authorization signatures self-describing
- keeps primitive conversion at HTTP/DB/integration boundaries

Transport DTOs may remain primitive.

---

## 20. Restore/rehydration is not a public business API

Persistence may use internal rehydration APIs and `InternalsVisibleTo(Rfq.Infrastructure)`.

Rationale:

- public arbitrary Restore would let Application bypass transitions
- Infrastructure genuinely needs to reconstruct persisted state
- a persistence DTO does not by itself solve access control

---

## 21. Use one semantic RFQ error classification

Expected operational failures share `ExpectedRfqException + RfqErrorKind` across Domain/Application/API/Bulk.

Unexpected invariants use `RfqInvariantException` / `DomainInvariantException`.

Rationale:

- HTTP status is transport policy, not exception-type policy
- Bulk continuation is orchestration policy, not exception-type policy
- BCL exceptions must not accidentally become expected 4xx errors
- one semantic classification prevents API and Bulk from disagreeing about the same failure

Do not put HTTP status, log level, retryability, alerting, or ContinueBulk flags on exception types.

---

## 22. ConfirmedQuote is immutable

Presentation, withdrawal, and expiry are separate events/state transitions and never rewrite the confirmed snapshot.

Rationale remains historical reproducibility and clear separation between “what was confirmed” and “what happened later.”

---

## 23. Amendment is a Draft Revision, not an RFQ status

A pending amendment coexists with the current confirmed Revision and quote until amendment Confirm.

Rationale:

- editing a future condition is not the same dimension as current customer-facing/quote state
- avoids composite statuses such as AmendingQuoted/AmendingPresented

---

## 24. Reopen does not resurrect a confirmed quote

Reopen produces Active + QuoteRequested(Reopened) + Unowned.

WorkingQuote values remain available for review/reconfirmation.

Rationale:

- old communicated quote may no longer be valid
- explicit reconfirmation is required
- retaining working values avoids re-entry

---

## 25. Revision Confirm invalidates old current quote only on Confirm

Draft amendment editing does not invalidate current confirmed RFQ/quote.

On amendment Confirm, current quote/presentation is cleared and state becomes Requested(Revised).

Rationale:

- Draft edits are not official conditions
- confirmation is the atomic business boundary

---

## 26. Presentation is RFQ/customer-facing state

Presented is controlled by Contact Owner and protects the quote from trader withdrawal.

It does not prove literal customer receipt.

Rationale:

- primary meaning is customer-facing workflow/protection, not pricing state
- therefore Present/Unpresent belong to `RfqLifecycleTransitions`

---

## 27. QuoteEvent does not store CaseId

QuoteEvent stores QuoteId and derives Case through ConfirmedQuote -> Revision -> Case.

Rationale:

- storing both CaseId and QuoteId permits mismatched references unless a composite consistency constraint exists
- join cost is acceptable at current scale
- denormalization can be reconsidered only if demonstrated performance requires it

---

## 28. Event cursor order must follow commit visibility

`GetEventsAfter` must not use a cursor scheme that can skip a lower ID committed later.

The infrastructure must keep a commit-order-safe mechanism and prove it with a real PostgreSQL reverse-commit concurrency test.

Rationale:

- persisted events are reconnect recovery/source of truth for update notification
- silent event loss is unacceptable

---

## 29. Current operational state is stored directly, not rebuilt from events

CaseCurrent remains a flattened persistence projection/state table; events remain audit/notification/history.

Rationale:

- UI/search needs fast current state
- event sourcing complexity is not justified
- persistence flattening does not require flattening Domain state

---

## 30. Lightweight Command/Query separation remains

Commands load typed business state and run Domain transitions.

Queries may directly join/project DB state into view DTOs.

Rationale:

- history/search reads do not need aggregate reconstruction
- update paths still require Domain invariants and atomic transactions
- no full CQRS framework is needed

---

## 31. Live follows authoritative queries; Paused preserves an explicit snapshot

Live Sales/Trader views catch up to authoritative server projections. Paused views deliberately preserve a display snapshot until explicit Refresh or resume.

Short-lived protected interactions defer replacement only for the unsafe interaction window; they do not turn ordinary Live work into a long-lived frozen snapshot.

Rationale:

- authoritative server state remains the single business source of truth
- Paused remains useful when an operator explicitly needs positional/display stability
- local editing safety is handled by short-lived protection rather than duplicating and manually maintaining Live business state
- frontend business-state prediction is avoided

---

## 32. SSE is wake-up transport, not authoritative data

SSE signals change; persisted event query performs catch-up.

Rationale remains safe reconnect and transport replaceability.

---

## 33. Master/pricing realism remains intentionally behind boundaries

The RFQ application does not reimplement holiday/convention/curve/pricing systems.

Rationale remains keeping workflow validation separate from the firm's pricing stack.

---

## 34. `CreateFromExisting` uses business date, not UTC calendar date

The “source Case created today” rule must compare desk/business dates.

Rationale:

- a JST desk can be on a different calendar date than UTC
- using `CreatedAt.UtcDateTime.Date` gives incorrect settlement-copy behavior near midnight/business-date boundaries

---

## 35. Keep two client-side change windows

The Changes UI keeps `Last Refresh` and `Pending Updates` rather than erasing all change information on Refresh.

Rationale:

- users can see what the most recent refresh incorporated
- two generations are sufficient operationally
- long-term audit remains persisted in Event tables

---

## 36. Separate update indication from important toasts

Most cross-session changes only mark Updates Available. Important transitions may additionally produce an in-app toast.

Rationale:

- desk-wide event volume can be high
- toast storms are worse than explicit refresh
- Close/Withdraw/Expiry/TakeOver-type changes may deserve immediate attention

---

## 37. Keep initial Past RFQ search simple

Use server query + indexes + approximately 20k-row cap and client-side grid sort/filter initially.

Rationale:

- expected data scale is manageable for PostgreSQL
- actual business query patterns are not yet fully known
- premature paging/materialized-view infrastructure would lock in assumptions

---

## 38. Persist all cross-session observable transitions

Any business transition another active session must discover through Pending Updates must append a persisted event in the same transaction as state mutation.

At minimum this includes quote confirmation as well as Revision Confirm, lifecycle transitions, ownership/responsibility transitions where notification is required, and Presentation/Withdraw/Expire.

Rationale:

- SSE is only a wake-up channel
- reconnect recovery depends on the persisted feed
- event coverage is determined by cross-session observability, not whether an event feels like audit history


---

## 39. Post Process is a staged operational worklist, not an EOD summary

Post Process replaces the earlier Daily Review/EOD placeholder.

It uses persisted Business Date facts, explicit visibility policy, FE-local staged changes, and per-Case atomic commit.

Rationale:

- operators may need to enter cleanup/memo changes after intraday work
- reviewing several intended changes before commit reduces accidental lifecycle edits
- Today semantics cannot be reconstructed reliably from quote/memo activity timestamps
- visibility and edit authority are different concerns

---

## 40. Business Date is persisted when it is a business fact

`CreatedBusinessDate`, `ClosedBusinessDate`, and relevant Event BusinessDate are explicit persisted values.

Rationale:

- UTC date is not desk business date
- timestamp-range queries are brittle around desk/date boundaries
- same-day outcome-correction rules need the original close business date, not a derived guess

---

## 41. Bulk public APIs remain operation-specific

Bulk use cases orchestrate single-item use cases and expose partial-success results, but there is no generic public bulk workflow engine.

Rationale:

- operation names remain business-readable
- single-item transitions stay the source of business rules
- per-item Unit of Work cleanup is an orchestration concern
- generic public bulk abstractions would erase useful business semantics

---

## 42. Typed user settings and opaque grid layout are intentionally different

Theme, Default Quote Mode, and Quote Expiry are typed API/business preferences.

Grid layout is versioned opaque frontend-owned JSON.

Rationale:

- semantic settings have a small closed value domain and deserve typed contracts
- grid layout shape is library/UI-specific and should not leak into backend business models
- a generic settings JSON bag would weaken type safety without helping grid layout

---

## 43. Theme colors are semantic, not literal

Light/Dark theme changes one application-level theme state used by Ant Design, AG Grid, custom CSS, and portal UI.

Business-state colors use semantic tokens.

Rationale:

- the same business meaning must remain recognizable across themes
- literal color names couple code to one palette
- two distinct meanings may currently share an RGB value but must remain independently changeable

Selection indication and pending-change indication are therefore distinct tokens even if their initial color is identical.

---

## 44. Post Process query refresh and pending intent are separate state

After a normal Post Process commit response, cached worklist queries reconcile with authoritative server state regardless of whether item results are Succeeded, Failed, or Skipped.

Local pending changes are removed only for Succeeded Cases.

Rationale:

- a VersionConflict means the displayed server row may already be stale
- retaining failed pending intent is useful to the operator
- refreshing authoritative data must not imply clearing the operator's unresolved intent


---

## 45. Domain boundary follows invariant ownership, not object count

Whether an operation belongs in Domain is determined by business invariants and coherent state transformation, not by whether it touches one object or several.

Rationale:

- multiple objects may form one business invariant
- a one-object operation may still require Application-only concerns such as current-user authorization or external I/O
- counting objects produces unstable boundaries as the model evolves
- the useful question is whether independently callable pieces could create an invalid business state

---

## 46. Business state data and business operations are deliberately separated

Entities/state objects primarily represent valid state; explicit transition/factory operations transform that state and return new values.

Rationale:

- business transitions remain discoverable and reviewable
- mutable member methods do not gradually become an uncontrolled public mutation surface
- transition grouping follows business vocabulary
- the model stays compatible with immutable state and optimistic concurrency

Member methods are not forbidden when they naturally belong to a value/factory/validation concern.

---

## 47. Organize source by ownership before framework artifact type

Backend/API/Application code remains grouped by business/use-case ownership. Global technical folders are reserved for genuinely cross-cutting concerns.

Frontend organization is page-oriented:

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

Page-specific components, controllers/hooks, models, column definitions, and helpers remain under their owning page and should be grouped by human-recognizable concepts such as `operations`, `search`, `pricer`, `grid`, or `work-pane` when those concepts are substantial enough to justify a folder.

Do not introduce page-local technical buckets such as generic `components/`, `hooks/`, `utils/`, and `types/` merely for classification.

`shared` is for concepts genuinely reused across pages. A top-level `features` area is introduced only when an independent user-facing feature genuinely spans pages; Sales/Trader/Post Process page slices are not themselves called features.

Rationale:

- a change to one business/page concept is easier to discover in one place
- framework-artifact buckets scatter one concept across the tree
- page ownership is simpler than strict multi-layer feature slicing at the current application size
- cross-page sharing should be earned by real reuse rather than shape similarity

---

## 48. CaseId is internal identity, not a business sequence

CaseId uses PostgreSQL bigint identity semantics and may have gaps.

Rationale:

- DB identity gives simple stable internal identity
- rollback/allocation gaps are normal and harmless
- humans may later need a separate formatted/reference number with different guarantees
- business meaning should not be inferred from an infrastructure identity

---

## 49. Hit is an RFQ outcome; Booking is downstream

Closing Hit terminates the RFQ workflow but does not make the RFQ aggregate a booking aggregate.

Rationale:

- quote/customer-decision workflow and trade booking have different lifecycle/integration concerns
- Booking may fail/reconcile independently after a legitimate RFQ Hit
- keeping the boundary explicit prevents booking concerns from distorting RFQ lifecycle types

---

## 50. Calculation does not own RFQ workflow state

Calculation may be a separate service and may own sophisticated pricing/reference-data logic, but RFQ Application remains authoritative for RFQ state, quote work persistence, authorization, and concurrency.

Rationale:

- pricing and workflow evolve for different reasons
- a calculation runtime should remain reusable outside this RFQ UI
- RFQ lifecycle must not become dependent on hidden state inside the pricing service

---

## 51. Operational usability is a design requirement

The UI is optimized for repeated desk operation rather than generic form-entry conventions.

Rationale:

- high-frequency Sales/Trader workflows benefit from dense grids, inline editing, keyboard access, and low-friction actions
- unnecessary modal confirmations and extra vertical chrome directly reduce throughput
- remote-update safety matters because silently replacing active work is operationally dangerous
- usability rules therefore belong in system design, not only in component styling
