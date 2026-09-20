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
