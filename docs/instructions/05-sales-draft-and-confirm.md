# Step 05 — Sales Draft Revision and Initial Confirm

## Goal

Complete the Revision model and initial Sales flow now that Client/Security/default routing are available:

```text
New -> optional Save Draft -> Confirm -> Open / Requested / Initial
```

## Read first

- canonical Revision sections in `01-domain-model.md`
- initial-confirm transitions in `02-state-transitions.md`
- Sales editing behavior in `04-ui-ux.md`
- rationale sections 1, 2, 3, 11, 13 in `10-design-decisions.md`

## Implement

### Domain

Extend the minimal Step 03 `RfqRevision` into the canonical Revision model:

```text
RevisionStatus:
- Draft
- Confirmed
- Superseded
- Discarded
```

Revision-owned fields:

- Notional
- SettlementDate
- StandardSettlementDate
- SalesAndTradingMessage

Add initial Confirm transition so Case moves from Draft lifecycle to Open with:

```text
RfqStatus = Active
QuoteStatus = Requested
QuoteRequestReason = Initial
CurrentRevisionId = confirmed revision
```

### Persistence

Add `RfqRevision`.

Add constraints supporting:

- at most one Draft Revision per Case
- current revision FK
- optimistic version where appropriate

Create migration.

### Application/API

Implement:

- update initial Draft Revision
- confirm initial Revision
- discard initial draft behavior as defined by canonical design

For direct Confirm from unsaved frontend state, API may create Case + Revision atomically in one use case if that is cleaner.

### Frontend

Sales form now uses the resolved/default-capable fields from Step 04:

- Client
- Security
- Notional
- SettlementDate
- Sales & Trading Message
- Contact Owner
- Assigned Trader

On Security selection, use the Step 04 defaults to populate Category, Assigned Trader, StandardSettlementDate, and initial SettlementDate.

Support:

- Save Draft
- direct Confirm
- editing saved Draft
- status display

## Important invariant

Confirming a Revision must satisfy the canonical invariant immediately: a confirmed Revision has a WorkingQuote.

Create the minimal `WorkingQuote` persistence/model shell in this step and implement `EnsureWorkingQuote(revisionId)` for the empty/default case. Step 07 extends the same model with calculated/manual payloads and quote-edit behavior.

**Do not use a no-op temporary WorkingQuote ensurer.** Initial Confirm must create/ensure the WorkingQuote in the same atomic use case.

Initial Confirm validation includes resolved Client/Security, Notional > 0, valid SettlementDate >= business today, Contact Owner, and Assigned Trader.

## Do not implement

- amendment of already Open Case
- trader ownership operations
- calculation
- quote confirmation
- presentation

## Completion criteria

Manual journey:

1. New RFQ
2. enter fields
3. Save Draft
4. reload page
5. edit Draft
6. Confirm
7. row becomes Active + Requested
8. reload page
9. state persists

Also verify direct Confirm without prior Save Draft.

## Tests

Domain:

- Draft -> Open initial transition
- invalid Confirm rejected
- Requested requires Initial reason

Infrastructure:

- partial unique Draft Revision constraint
- confirmed Revision has exactly one WorkingQuote shell
- CurrentRevisionId FK and lifecycle/state persist consistently

API/Application:

- Save Draft
- Confirm initial
- direct Confirm path

Frontend:

- status change reflected after Confirm

## Commit boundary

```text
add revision model and initial rfq confirmation
```
