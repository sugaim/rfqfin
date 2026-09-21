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
