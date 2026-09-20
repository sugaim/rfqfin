# Step 08 — ConfirmedQuote and Presentation

## Goal

Allow Trader to Confirm a WorkingQuote into an immutable ConfirmedQuote, then allow Contact Owner to Present/Unpresent it.

## Read first

- ConfirmedQuote and Presentation sections in `01-domain-model.md`
- quote/presentation transitions in `02-state-transitions.md`
- authorization rules
- Design Decisions 4 and 14

## Implement

### ConfirmedQuote

Persistence supports Revision -> 0..N immutable ConfirmedQuotes.

Quote Confirm:

- requires lifecycle Open
- requires `QuoteStatus = Requested`
- requires owner trader
- requires WorkingQuote belonging to the current Revision
- requires expected WorkingQuote/CaseCurrent versions to match
- requires no currently valid current ConfirmedQuote
- does **not** recalculate
- snapshots current WorkingQuote
- stores calculation/manual mode and required context
- sets current quote reference
- emits/persists a `QuoteEvent: Confirmed` once event persistence exists; until Step 12, keep an explicit application event seam so Step 12 can backfill this transition without changing its business behavior
- updates:
  - QuoteStatus = Quoted
  - QuoteRequestReason = null

For expiry selection, store enough data to support Step 11:

- Trader default expiry setting (None or N minutes) in development User/preferences data
- per-RFQ override before Confirm
- expiry setting captured on ConfirmedQuote
- resolved ExpiresAt when applicable

### Presentation

Contact Owner can:

```text
Active + Quoted -> Presented + Quoted
Presented + Quoted -> Active + Quoted
```

Trader cannot Withdraw while Presented, although Withdraw itself arrives in Step 11.

### Frontend

Trader:

- Confirm Quote action
- expiry selector: None or N minutes
- clear visual indication of current confirmed quote
- quote cells locked while `QuoteStatus = Quoted`; they become editable again only after Revised/Reopened/Expired/Withdrawn returns the RFQ to Requested

Sales/Contact Owner:

- Present
- Unpresent

Do not imply Present means customer technically received anything.

## Completion criteria

1. Trader calculates WorkingQuote
2. Confirm creates immutable snapshot
3. changing WorkingQuote later does not mutate old ConfirmedQuote
4. Contact Owner Presents
5. status becomes Presented
6. Unpresent returns to Active/Quoted

## Tests

- Confirm does not invoke calculation client
- immutable historical quote remains unchanged
- only owner trader confirms
- only Contact Owner Presents/Unpresents
- Present requires current valid quote
- second Confirm while a valid current quote is already active is rejected
- Quoted WorkingQuote is not editable

## Commit boundary

```text
add quote confirmation and presentation
```
