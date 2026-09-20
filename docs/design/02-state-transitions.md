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
