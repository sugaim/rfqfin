# Step 10 — Amendment and Requote

## Goal

Implement amendment without introducing an `Amending` RFQ status.

## Read first

- Revision behavior in `01-domain-model.md`
- amendment transitions in `02-state-transitions.md`
- Sales UX in `04-ui-ux.md`
- Design Decisions 2 and 13

## Implement

For an Open RFQ:

- editing Revision-owned fields creates/updates one Draft Revision
- current Confirmed Revision remains operational
- current quote remains operational while Draft exists
- Trader cannot see Draft contents
- Sales sees changed-cell indication

On amendment Confirm:

- previous current Confirmed -> Superseded
- Draft -> Confirmed
- CurrentRevisionId changes
- ensure WorkingQuote for new Revision
- RfqStatus -> Active
- QuoteStatus -> Requested
- QuoteRequestReason -> Revised
- current quote association removed
- if previously Presented, no longer Presented

On Discard:

- Draft -> Discarded
- current Confirmed Revision remains current
- live quote/status remain unchanged

Implement copying a historical condition into a new Revision where reasonable:

- `CopiedFromRevisionId`
- `QuoteSeedRevisionId`

Do not reactivate an old Revision.

### Create New from Existing

Implement the canonical `CreateFromExisting` use case. It always creates a new Case.

Copy:

- Client
- Security
- Notional
- Sales & Trading Message

Do not copy Sales-only Memo, Trader-only Memo, quote lifecycle/state, or old ownership.

Initialize Contact Owner/Sales/routing as a new Case, rerun Assigned Trader routing, apply the canonical settlement rule (source created today -> copy actual settlement; older source -> current standard settlement), and persist `CopiedFromCaseId` as provenance only.

## Frontend

Sales grid/edit pane:

- immediate server autosave after committed cell edit on existing Open RFQ
- visual highlight of changed cells
- Confirm Amendment
- Discard Amendment
- Confirm Selected / Discard Selected with per-Case results; one conflict must not fail unrelated selected Cases
- Create New from Existing

Trader view continues to show only current Confirmed data until Confirm.

## Completion criteria

Manual:

1. start with Quoted RFQ
2. Sales edits Notional
3. Trader still sees old confirmed Notional/quote
4. Sales Discards -> no status change
5. Sales edits again and Confirms
6. Trader sees new Revision
7. QuoteStatus becomes Requested/Revised
8. WorkingQuote exists for new Revision

## Tests

- only one Draft per Case
- Draft does not affect current quote
- Confirm supersedes old Revision atomically
- Discard behavior
- quote seeding from historical Revision
- bulk Confirm/Discard partial success
- CreateFromExisting copy/non-copy/routing/settlement rules

## Commit boundary

```text
add amendment and requote workflow
```
