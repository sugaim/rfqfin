# Step 07 — WorkingQuote and Mock Calculation

## Goal

Add the trader's editable pricing workflow backed by a Revision-owned WorkingQuote and deterministic mock calculation.

Do not Confirm the quote yet.

## Read first

- WorkingQuote section in `01-domain-model.md`
- quote calculation model in `06-calculation-and-search.md`
- calculation failure logging in `05-persistence-and-events.md`
- Design Decisions 10 and 11

## Implement

### Domain

Add Revision-owned `WorkingQuote`.

Support:

- Calculated mode
- Manual mode
- current calculated payload
- current manual payload
- optimistic version

Implement `EnsureWorkingQuote(revisionId)` behavior:

1. return existing
2. clone configured seed Revision WorkingQuote if specified
3. otherwise create empty/default

### Calculation boundary

Create:

```text
ICalculationClient
MockCalculationClient
```

Use the canonical bulk request/response shape concept:

- requestId
- securityId
- settlementDate
- calculation type / driver
- typed parameter

Responses are per-item Success/Error.

Mock must be deterministic and plausibly shaped, but need not be financially correct.

Calculated flow must support:

- edited cell implies driver
- base Simple Yield
- trader-entered `SimpleYieldSlide`
- final Simple Yield = base + slide
- representative output fields needed by grid

### Failure behavior

Calculate before short DB write transaction.

Before writing a successful result, re-read/revalidate the command-side current state: expected `CaseCurrent` version/current Revision, owner authorization, and WorkingQuote version must still match the state against which calculation was requested. A calculation started for an old Revision or old owner must not write back after Sales confirms a new Revision or ownership changes.

On calculation failure:

- WorkingQuote remains unchanged
- return typed CalculationFailure error
- persist `CalculationFailureLog`
- frontend reverts edit and shows toast

### Manual mode

Switching to Manual:

- clears Manual values
- retains Calculated state

Switching back restores retained Calculated state.

Manual mode has independent:

- Price
- Final Simple Yield

Do not enforce consistency between them.

### Frontend

Trader grid gains editable quote columns.

Only owner trader can edit.

Add mode switch and a visible calculation status/error behavior.

## Completion criteria

Manual:

1. pick up RFQ
2. edit Price -> calculated outputs change
3. edit another driver -> correct driver is sent
4. edit Slide -> final simple yield changes
5. force mock failure -> cell reverts and failure toast appears
6. switch Manual -> values blank
7. enter manual values
8. switch back -> previous calculated state returns

## Tests

- EnsureWorkingQuote
- mode switching
- calculation success update
- calculation failure leaves DB WorkingQuote unchanged
- optimistic WorkingQuote version conflict
- calculation result rejected if CurrentRevision changed while calculation was in flight
- calculation result rejected if ownership/CaseCurrent version changed while calculation was in flight
- failure log persisted

## Commit boundary

```text
add working quote and mock calculation workflow
```
