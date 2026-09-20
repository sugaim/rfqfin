# Step 09 — Hit/Away, Contact Owner, and Memos

## Goal

Complete the normal happy-path RFQ lifecycle:

```text
Sales create -> Trader quote -> Present optional -> Hit/Away
```

and add Contact Owner handoff plus case memos.

## Read first

- Contact Owner, Close, Memo sections in `01-domain-model.md`
- close transitions in `02-state-transitions.md`
- authorization matrix
- EOD notes only for context; EOD UI comes later

## Implement

### Close

Contact Owner may close an Open RFQ with a valid current quote:

- Hit
- Away

Presentation is not required.

On close:

- lifecycle -> Closed
- RfqStatus -> Hit or Away
- `ClosedQuoteId` records quote in `CaseCurrent`
- current operational quote association is no longer used for open workflow
- Owned -> false
- pending Draft Revision -> Discarded
- historical quotes/WorkingQuote retained

Add explicit outcome correction:

```text
Hit <-> Away
```

No silent rewriting during bulk operations.

### Contact Owner

Support changing Contact Owner between valid Sales/Trader users.

Require explicit confirmation in UI.

### Memos

Add Case-level:

- Sales-only Memo
- Trader-only Memo

They remain editable after close according to role.

Do not confuse these with Revision-owned SalesAndTradingMessage.

### Frontend

Sales/Trader details expose appropriate memo.

Contact Owner controls:

- Hit
- Away
- Bulk Hit / Bulk Away for selected rows, with per-item results; already Closed rows are skipped and never have their outcome silently changed
- Contact Owner handoff
- outcome correction when Closed

## Completion criteria

Complete a full RFQ from create through Hit and another through Away.

Verify a closed RFQ cannot be operated as Open.

Verify memo permissions and post-close editing.

## Tests

- close preconditions
- ClosedQuoteId
- pending Draft discard
- ownership cleared
- outcome correction
- bulk close skips already Closed rows and reports per-item result
- Contact Owner authorization
- role-scoped memo editing

## Commit boundary

```text
complete hit away and contact owner workflow
```
