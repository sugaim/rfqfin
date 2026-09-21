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
