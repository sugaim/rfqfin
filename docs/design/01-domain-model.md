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
