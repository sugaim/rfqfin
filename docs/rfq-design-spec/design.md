# JPY Corporate Bond RFQ System — Canonical Design

This directory is the canonical design specification for the internal JPY corporate bond RFQ system.

It records:

- domain data and invariants
- business state transitions
- application/use-case boundaries
- authorization boundaries
- UI behavior
- persistence/event design
- calculation/search boundaries
- runtime assumptions
- test/seed strategy
- explicit non-goals and rationale

It deliberately does **not** prescribe a historical implementation sequence. Implementation instructions may describe how to reach this design, but if an implementation instruction conflicts with this canonical design, **this canonical design wins unless the design is explicitly revised**.

## Document map

1. [01-domain-model.md](01-domain-model.md) — entities, immutable domain data, lifecycle states, revisions, quotes, ownership, category, memos, versions.
2. [02-state-transitions.md](02-state-transitions.md) — business transition categories and transition rules.
3. [03-use-cases-and-authorization.md](03-use-cases-and-authorization.md) — Application/Domain split, authorization, commands/queries, concurrency, errors.
4. [04-ui-ux.md](04-ui-ux.md) — Sales/Trader/EOD screens, grid behavior, refresh/change tracking, search, pricer, and draft UX.
5. [05-persistence-and-events.md](05-persistence-and-events.md) — logical schema, mapping, event persistence, transactions, constraints.
6. [06-calculation-and-search.md](06-calculation-and-search.md) — calculation boundary, WorkingQuote update flow, security/client search, defaults.
7. [07-runtime-and-notifications.md](07-runtime-and-notifications.md) — ASP.NET runtime, SSE wake-up, event retrieval, expiry worker, observability.
8. [08-testing-and-seed.md](08-testing-and-seed.md) — domain/application/infrastructure tests and seed data.
9. [09-scope-and-deferred.md](09-scope-and-deferred.md) — explicit non-goals and future design space.
10. [10-design-decisions.md](10-design-decisions.md) — rationale for non-obvious choices.

A concatenated convenience copy is also provided as [design.md](design.md).

## Design principles

- Domain data is immutable from callers and represents valid business state.
- Use types to make important invalid RFQ/quote state combinations unrepresentable where practical.
- Classify Domain transitions by **business transition category**, not by the number of objects they touch.
- Application is the Use Case layer: it loads, authorizes, allocates IDs/time, invokes Domain transitions/factories, persists, emits events, and commits atomically.
- Actor/role authorization is centralized in Application; state validity belongs to Domain transitions.
- Confirmed business facts are immutable snapshots.
- Persistence shape may be flattened and does not need to mirror the typed Domain hierarchy.
- Use typed IDs and value objects inside Domain/Application; convert to raw transport/persistence primitives at boundaries.
- Use optimistic concurrency and transactional use cases rather than long-lived locks.
- Do not make the browser silently replace actively edited data.
- Preserve history/provenance sufficient for reconciliation and investigation.
- Prefer replaceable boundaries over speculative generalization.


---

# 01. Domain Model

## 1. RFQ Case identity

An **RFQ Case** represents one customer inquiry that ultimately closes with one Hit/Away outcome.

Boundary rule:

- if a new condition **replaces** a previous condition, it is the same Case with a new Revision
- if two conditions can coexist and independently result in Hit/Away, they are separate Cases
- future List / Thread / Portfolio grouping lives outside the Case

Stable Case identity:

- `CaseId`
- `ClientId`
- `SecurityId`

Changing Client or Security means creating a new Case, not a Revision.

Case-level facts/current responsibility include:

- `CreatedAt`
- `CreatedBy`
- `SalesId?`
- `CategorySnapshot : CategoryId`
- `CopiedFromCaseId?`
- `ContactOwnerId`
- `AssignedTraderId`
- `Version : StateVersion`
- source metadata if external sources are added later

`ContactOwnerId` and `AssignedTraderId` are Case-level values. Do not duplicate them inside lifecycle subtypes.

---

## 2. Immutable Domain data

Business-state objects are immutable from callers.

In particular:

- `RfqCase`
- `RfqRevision`
- `WorkingQuote`
- `ConfirmedQuote`
- `CaseMemo`
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

Terminal Hit/Away state and carries the `ClosedQuoteId` used for the outcome.

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

A Revision owns the customer condition set. A useful value object is:

```text
RevisionTerms
- Notional
- SettlementDate
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

Revision provenance may include:

- `CopiedFromRevisionId?`
- `QuoteSeedRevisionId?`

The Case property must be named according to its semantics: use `CurrentRevision`, not a mutable property called `InitialRevision` that later contains amendments.

---

## 7. WorkingQuote

A `WorkingQuote` belongs to exactly one Revision.

Logical cardinality:

```text
Revision -> 0..1 WorkingQuote
```

Persisted operational intent is that a confirmed/current Revision has one WorkingQuote.

`WorkingQuote` is immutable Domain data and is changed only through `WorkingQuoteTransitions`.

It remains:

- versioned using `StateVersion`
- the last successful calculated state once populated
- retained across Withdraw, Expire, Cancel, and Reopen
- attached to its historical Revision

Payloads:

- Calculated payload
- Manual payload
- active mode

Switch Calculated -> Manual:

- Manual values start empty
- Calculated values remain retained separately

Switch back:

- previous Calculated state is restored as active

### WorkingQuote factory

WorkingQuote creation is performed by a Domain factory, not by mutating a repository row.

For initial Confirm, Application calls the RFQ lifecycle transition, then creates the initial WorkingQuote for the confirmed/current Revision, and persists both in one transaction.

For amendment Confirm, create a new WorkingQuote for the new Revision; seed from `QuoteSeedRevisionId` when specified. Never reuse/reactivate the old Revision's WorkingQuote as the new Revision object.

The DB still enforces one WorkingQuote per Revision.

---

## 8. ConfirmedQuote

A `ConfirmedQuote` is an immutable snapshot created by the **Quote Confirm Domain transition**.

Logical cardinality:

```text
Revision -> 0..N ConfirmedQuote
```

It contains, as applicable:

- `QuoteId`
- `RevisionId`
- `SecurityId`
- `SettlementDate`
- `ConfirmedBy` / `ConfirmedAt`
- quote mode (`Calculated` or `Manual`)
- driver/input values
- simple-yield slide for close-based calculated quotes
- output values such as price, yields, spreads, accrued, settlement amount, A/L, delta
- calculation context / market context
- expiry policy
- resolved `ExpiresAt?`
- request reason being answered, where useful for audit/search

`ConfirmedQuote` is not independently made current by Application code. Quote confirmation must create the snapshot and update RFQ current quote state as one coherent Domain operation.

It does not mutate when later Presented, Withdrawn, or Expired.

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

### Sales-only Memo / Trader-only Memo

- Case-owned
- independent of Revision
- editable after Close
- `CaseMemo` is immutable Domain data
- updates are performed by `CaseMemoTransitions`

Memo changes are not RFQ lifecycle transitions.

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

Use for Case current state, Revision, WorkingQuote, and CaseMemo.

Infrastructure/API may map to raw `long` at boundaries.

---

## 16. Create New from Existing

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

Canonical transition groups:

```text
RfqLifecycleTransitions
RfqOwnershipTransitions
QuoteTransitions
AmendmentTransitions
WorkingQuoteTransitions
CaseMemoTransitions
```

Use an explicit transition for Contact Owner handoff and for initial-Draft editing; do not fall back to public mutable setters or a generic public Revision transition that bypasses Case rules.

Application loads state, authorizes, supplies IDs/time/business date, invokes transitions/factories, persists all results, records events, and commits atomically.

---

## 2. Initial creation

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
- lifecycle = `DraftRfq`
- Case ID assigned
- subsequent Draft edits autosave through an explicit initial-Draft Domain transition

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

Editing Revision-owned fields on a confirmed/open RFQ creates or updates the one pending Draft Revision.

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

Effects:

```text
OpenRfq -> ClosedRfq
ClosedQuoteId = current quote
Outcome = Hit | Away
```

Also:

- pending Draft Revision is automatically Discarded
- WorkingQuote retained as history
- ConfirmedQuote remains immutable
- Case-level Assigned Trader retained
- Case-level Contact Owner retained

### Bulk Close

Bulk Hit/Away closes only Cases that are still eligible/open.

Already Hit/Away Cases are skipped; bulk Close never silently changes an existing outcome.

### Outcome correction

Explicit transition only:

```text
Closed(Hit) <-> Closed(Away)
```

Case remains Closed; append outcome-correction history/event.

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

## 13. CaseMemo transitions

`CaseMemoTransitions.UpdateSales` and `.UpdateTrader`:

- validate expected `StateVersion`
- normalize memo text consistently
- return new `CaseMemo`
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


---

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

### `CaseMemo`

1:1 per Case initially.

Contains SalesMemo, TraderMemo, Version, and any needed audit metadata.

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
- event cursor / Event tables

---

## 3. StateVersion mapping

Domain/Application use `StateVersion`.

DB remains signed `bigint`/`long` and EF concurrency token.

Map at the Infrastructure boundary.

Do not change DB columns to unsigned types.

---

## 4. Domain rehydration

Persistence reconstruction must not require public mutable setters or public arbitrary `Restore` escape hatches.

Use non-public constructors / `internal Restore` or equivalent and narrowly allow Infrastructure access, e.g. `InternalsVisibleTo("Rfq.Infrastructure")`.

Application code should use public factories/transitions, not rehydration APIs.

If persisted columns represent an impossible combination, mapping should fail as a Domain invariant/data-integrity problem rather than silently constructing invalid state.

---

## 5. Event persistence

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
Payload jsonb
```

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

## 6. Event types

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

## 7. Authoritative data vs projection

Authoritative business data includes:

- RfqCase / lifecycle state as persisted through tables
- RfqRevision
- WorkingQuote
- ConfirmedQuote
- CaseMemo
- Event / RfqEvent / QuoteEvent
- configuration/master data
- calculation failure log

`CaseCurrent` is the transactionally maintained current operational projection/flattened persistence slice.

Do not rebuild it from events on every request.

---

## 8. Repository boundaries

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

## 9. Unit of Work / transactions

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

## 10. Loading strategy

Command-side retrieval loads only state needed for the transition:

- Case facts/current lifecycle
- Current Revision
- pending Draft if relevant
- WorkingQuote/current ConfirmedQuote/seed WorkingQuote as relevant

Do not routinely load all historical Revisions, all quotes, or all Events.

History/search uses query-side DTOs.

---

## 11. DB constraints

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

## 12. Indexing and search projection

Keep current pragmatic indexing strategy for expiry worker and Past RFQ search.

Do not create broad speculative compound indexes or a full copied search model until usage requires it.

A future thin search projection may store references, but do not duplicate every display field prematurely.

---

## 13. Initial indexing

Initial indexes should cover actual operational queries, including:

- expiry worker: current confirmed/quoted items + `ExpiresAt`
- Past RFQ search by date/client/security/category/contact owner/assigned trader/status
- stable PK/FK joins

Do not pre-create every possible compound index. Observe real search patterns and add targeted indexes.

---

## 14. Past RFQ read model

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

The RFQ application does not own detailed pricing conventions, curve resolution, or security convention logic.

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

Keep four concerns separate.

### Domain/business audit

Use persisted:

- RfqEvent
- QuoteEvent

### Business calculation failure

Use:

- CalculationFailureLog

### Technical logging

Use `ILogger<T>`.

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
- CaseMemo transitions
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

Authorization is centralized so Manager overrides can be added later; policy is deferred.

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
- no auto-Away at EOD
- no automatic UI refresh of main RFQ data
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
- actual EOD procedure
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

Use one common `StateVersion` for Case current state, Revision, WorkingQuote, and CaseMemo.

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

## 21. Use a small Domain exception taxonomy

Use categories such as:

- DomainRuleViolationException
- DomainValidationException
- StateVersionMismatchException
- DomainInvariantException

Do not create exception subclasses for every operation.

Rationale:

- callers/API need to distinguish broad handling categories
- hundreds of micro-exceptions add maintenance without value
- invariant/data-corruption failures must not be mistaken for normal user validation

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

## 31. Do not auto-refresh active grids

Server changes mark updates available; user Refresh applies authoritative state.

Rationale remains avoiding invisible replacement of actively edited rows.

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
