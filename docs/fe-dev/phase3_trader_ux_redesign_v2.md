# Phase 3 — Trader Workspace UX Redesign

## Goal

Redesign the Trader workspace around a dense, stable, front-office operating model.

Baseline:

- repository: `sugaim/rfqfin`
- baseline commit: `075e2cdb078a2ab4eb9778883341ae2b73f3c83e`
- frontend: `src/Rfq.Web`
- current Trader feature:
  - `src/Rfq.Web/src/features/trader/TraderWorkspace.tsx`
  - `src/Rfq.Web/src/features/trader/TraderScreen.tsx`

This phase is primarily a **Trader UX redesign**. It may make the **minimal backend/API/read-model extensions required by the agreed Trader UX**, but it must not redesign unrelated Domain lifecycle semantics.

The target Trader screen is a high-density RFQ workstation with:

1. a compact stable toolbar;
2. a large Active RFQ Grid as the main working surface;
3. a collapsible/resizable historical RFQ Search area below the Active Grid;
4. one collapsible/resizable right-side pane with exactly two tabs:
   - `Operations`
   - `Pricer`
5. a compact bottom Result Bar for bulk-operation results.

The screen should feel closer to a trading blotter / spreadsheet than to a form-heavy workflow application.

---

## 1. Scope and non-goals

Implement the agreed Trader workspace UX.

Do not redesign in this phase:

- Daily Review;
- Booking integration;
- post-trade workflow;
- generic RFQ detail pages;
- full Revision History / Quote History UI;
- a permanent generic event/change-history panel;
- a generic application-wide command bus;
- Domain lifecycle semantics;
- Bulk Hit support.

Revision and Quote history APIs may remain available, but **history UI is deliberately lower priority and is not required for this phase**.

Do not copy Sales workflow colors or Sales Work Pane semantics into Trader.

Sales and Trader may share low-level interaction primitives where useful, but they have different workflow emphasis.

---

## 2. Overall screen structure

Target structure:

```text
+----------------------------------------------------------------------------------+
| [Pick (N)] [Confirm] [Ops] [Pricer]                    [Live/Paused]        [↻]  |
+---------------------------------------------------------------+------------------+
|                                                               |                  |
|                     Active RFQ Grid                           |   Operations /   |
|                                                               |      Pricer      |
|                                                               |   right pane     |
+================ draggable horizontal splitter ================+                  |
| RFQ Search                                                    |                  |
| +------------------+----------------------------------------+ |                  |
| | collapsible      | Search Result Grid                     | |                  |
| | filter panel     |                                        | |                  |
| +------------------+----------------------------------------+ |                  |
+---------------------------------------------------------------+------------------+
| Result Bar                                                                      |
+----------------------------------------------------------------------------------+
```

Rules:

- Active RFQ Grid is the main battlefield and receives most vertical space.
- RFQ Search is auxiliary and initially occupies roughly the lower 25–30% of the left workspace.
- The Active/Search boundary is vertically resizable.
- RFQ Search may be collapsed so Active Grid can use nearly the whole left workspace.
- The Search filter panel is on the left of Search results and may itself be collapsed.
- The right pane is one physical pane.
- `Operations` and `Pricer` are tabs inside that same pane.
- Operations and Pricer are **never shown side-by-side**.
- The right pane is collapsible and its width is resizable.
- Do not restore the current separate `Case details` card below the Grid.
- Do not keep the current Pricer as an overlay Drawer.

Do not create a permanent lower history panel.

---

## 3. Toolbar

Keep the Trader toolbar compact and spatially stable.

Primary layout:

```text
[Pick (3)] [Confirm] [Ops] [Pricer]                       [Live] [↻]
```

Use short labels.

### `Pick (N)`

`N` is the number of RFQs satisfying:

```text
AssignedTraderId == current trader
AND Ownership == Unowned
```

Pressing `Pick (N)` bulk-picks **all** currently eligible RFQs matching that condition.

This action is independent of current row selection.

Do not show a confirmation modal.

Use the existing bulk Pick Up backend operation.

Partial success/failure is reported through the Result Bar.

If `N == 0`, the button may be disabled while remaining visible.

### `Confirm`

Opens the Quote Confirmation summary for the currently selected **confirmable** rows.

Do not move or replace this button based on selection count.

### `Ops`

Opens the right pane and selects the `Operations` tab.

### `Pricer`

Opens the right pane and selects the `Pricer` tab.

### `Live / Paused`

Compact refresh-mode control. See the dedicated refresh section.

### Refresh

Use an icon-only refresh control at the **far right**.

Tooltip:

```text
Refresh
```

Do not use a large text `Reload` button.

---

## 4. Selection model

Use an Excel-like row-selection model.

Required behavior:

```text
click           -> select one row
Ctrl/Cmd+click  -> add/remove row from selection
Shift+click     -> range selection
```

Do not use visible selection checkboxes as the normal UX.

The selected rows are the target for bulk-capable actions.

### Selection appearance

Business-state attention coloring and selection must not compete.

Use:

```text
row background -> workflow / attention
left marker    -> selection
```

Selected rows receive a narrow marker at the left edge.

Do not add a selection background overlay that changes the meaning of the attention color.

Cell focus may continue to use AG Grid's normal focus indication.

### Cell editing

Do not use single-click edit for quote cells.

Preferred edit start:

- `Enter`
- `F2`
- double-click

`Esc` cancels the active cell edit.

Committing and moving to another cell/row should feel spreadsheet-like, but do not create a custom spreadsheet engine.

---

## 5. Trader attention coloring

Trader row coloring is based primarily on **work/attention required**, not on Sales lifecycle coloring.

Highest attention:

```text
AssignedTraderId == me
AND Ownership == Unowned
```

This means the ticket has been routed to the Trader but has not yet been picked up / acknowledged.

Second-level attention:

```text
Owned by me
AND QuoteStatus == Requested
```

This means the Trader has already acknowledged the ticket but still has pricing work.

Do not add strong background coloring merely to indicate calm states such as:

```text
Quoted
Presented
```

Those states may use compact text/badges without an attention background.

Rows owned by another Trader should normally remain visually quiet.

Terminal rows may be subtly dimmed if they appear in a projection, but do not let terminal styling dominate the screen.

### Sorting

Do **not** automatically move attention rows to the top in this phase.

Do not reorder rows merely because:

- ownership changed;
- a quote was confirmed;
- the RFQ state changed.

User-selected sorting remains authoritative.

A future explicit `Needs Attention` preset may be added later if actual usage shows it is necessary.

---

## 6. Active Grid column organization

Use grouped columns and compact labels.

Short labels are explicitly acceptable:

```text
Case
Notl
Px
Yld
SY
YSC
GSpd
ASW
ISpd
ZSpd
Req
Ini
Rev
Wdr
Exp
```

Units do not need to be repeated in every visible header when the unit is obvious to desk users.

Use header tooltip/help text for explicit units such as:

```text
Yield (%)
Simple Yield (%)
YSC (bp)
G-Spread (bp)
```

`Notl (MM)` should remain explicit because notional unit mistakes are more dangerous.

### Default column groups

Use approximately:

```text
RFQ
Routing / State
Quote
Info
```

Suggested initial order:

```text
RFQ:
  Case
  Client
  Security
  Notl (MM)
  Settle

Routing / State:
  Trader
  State

Quote:
  Mode
  Px
  Yld
  SY
  Final SY
  YSC
  GSpd
  ASW
  ISpd
  ZSpd
  Slide

Info:
  Elapsed
  Msg
  Trader Memo
  Calc
```

`Case` must be visible by default.

Case ID is an important human communication identifier and is not a hidden/internal-only field.

Do not force every supported quote metric to be visible forever; use Grid layout configuration for additional metrics. However, the initial Trader layout should expose the desk-relevant quote metrics rather than hiding almost everything.

---

## 7. Compact Trader state display

Do not expose three verbose status columns by default when one compact Trader-oriented state column can communicate the work state.

A single `State` column may render combinations such as:

```text
Req/Ini
Req/Rev
Req/Wdr
Req/Exp
Req/Reopen
Quoted
Presented
Cancelled
Hit
Away
```

Exact abbreviation may be adjusted for readability.

The internal source of truth remains the typed RFQ / Quote state.

This is only a presentation projection.

Likewise, Assigned Trader and Ownership may be rendered compactly in one Trader-oriented presentation where useful, for example:

```text
Me · New
Me · Owned
Trader B · Owned
```

Do not remove the underlying fields from the API solely because the UI combines them.

---

## 8. Trader read-model additions

The current Trader read model is missing fields needed by the agreed UI.

Extend it minimally to provide at least:

- `SalesAndTradingMessage`
- `StateSince`

`StateSince` supports the `Elapsed` display.

`SalesAndTradingMessage` is the Revision-owned message and is **not** Trader Memo.

Do not expose Sales Memo to the Trader screen as part of this phase.

Trader text concepts are:

```text
Msg         -> current Revision SalesAndTradingMessage
Trader Memo -> independent Trader Memo business data
```

Keep them distinct.

---

## 9. Quote editing — general

The main Active Grid is the primary quote-editing surface.

Calculated-mode quote metrics that are valid calculation drivers are inline editable.

Editing a driver cell means:

```text
edited cell becomes CalculationDriver
edited value becomes driver value
```

The successful calculation updates the entire calculated Quote payload shown in the row.

Do not update only the edited cell.

### Initial driver family

The existing implementation currently supports only a subset.

The target UX requires support for at least the desk-relevant family including:

```text
Price
BBG Yield
Simple Yield
YSC
G-Spread
```

The UI should also be structured to display/use the agreed spread-family metrics:

```text
ASW
I-Spread
Z-Spread
```

Where these are driver-capable in the calculation boundary, make them editable consistently.

Do not invent production financial formulas in the frontend.

If the current development calculation client is a mock/stub, extend that boundary deterministically for the required fields rather than embedding pricing formulas into React.

The Domain/API calculation types should remain the authoritative typed representation.

---

## 10. Simple Yield Slide

`Slide` is independent of `CalculationDriver`.

Editing Slide does **not** make Slide the driver.

Instead:

```text
retain current CalculationDriver
retain current driver value
recalculate using the new Slide
```

Do not duplicate driver semantics into Slide.

---

## 11. One calculation in flight per RFQ

Do not lock the entire Trader screen during calculation.

Do not block unrelated RFQs.

For a single Case, allow at most one Working Quote calculation request to be in flight at a time.

While calculation is in flight for Case X:

- quote-edit cells for Case X should not start another calculation;
- other Cases remain fully usable;
- row selection remains usable;
- Search remains usable.

The calculation service is expected to respond quickly, so this should normally be barely noticeable.

Do not initially implement a multi-request queue per RFQ.

---

## 12. Calculation timeout / stale response protection

A failed or unavailable calculation server must not leave the UI permanently locked.

Provide a bounded calculation wait.

On timeout or failure:

- clear the local calculating lock;
- preserve/revert the last valid Working Quote;
- mark the Calc status as failed;
- allow subsequent operator actions.

Manual Refresh is an explicit escape hatch.

If a refresh invalidates a pending local calculation state, a later stale response must not overwrite the newly loaded screen state.

Use request identity/generation tracking or an equivalent simple mechanism.

Do not let response arrival order corrupt the displayed Working Quote.

---

## 13. Calc status column

Keep a compact `Calc` status column near the right side of the Active Grid.

Normal success/idle state should be visually quiet.

Useful states:

```text
idle / blank
calculating
failed
```

For calculating:

- show a small spinner or compact indicator.

For failure:

- show a visible error indicator;
- expose details through tooltip/popover.

Failure details should include, where available:

```text
error code
message
request/correlation id
Calculation Failure Log ID
```

The backend already persists `CalculationFailureRecord` and throws `CalculationFailureException` with `FailureLogId`.

Extend the API error payload as necessary so the frontend can display the failure/log identifier.

Do not make support staff infer the failure solely from a generic toast.

---

## 14. Manual mode

Working Quote mode remains:

```text
Calculated
Manual
```

Mode is visible in the Quote group using a compact representation such as:

```text
Calc
Man
```

Changing mode must preserve the existing Domain semantics:

### Calculated -> Manual

- Manual payload starts/continues independently.
- Calculated payload remains retained.
- calculated numeric outputs are not presented as the active manual quote.
- Manual `Px` and `Final SY` are the editable values.

### Manual -> Calculated

- previously retained calculated values become visible again.

Manual Quote confirmation requires the existing required manual fields.

Do not erase the retained calculated payload when switching to Manual.

---

## 15. Default Quote Mode user setting

Add a Trader personal setting:

```text
Default Quote Mode
  Calculated
  Manual
```

Default for users without a saved preference:

```text
Calculated
```

This is a personal preference, not a Grid layout value.

It applies to newly created Working Quotes associated with that Trader.

It must not retroactively switch existing Working Quotes.

Use a small user Preferences/settings surface; do not put this setting permanently in the Trader toolbar.

The existing `/api/me/settings/...` pattern is the preferred API location.

Keep the setting typed.

At Working Quote creation time, resolve the preference for the Assigned Trader; fall back to Calculated when no preference exists.

---

## 16. Trader Memo

Trader Memo is important and must be directly usable from the Active Grid.

Use inline editing.

Default cell appearance:

- one-line compact preview;
- ellipsis for long values;
- tooltip may show full value.

Editing:

- start using normal explicit cell-edit gesture;
- use a textarea-like editor where useful;
- `Ctrl+Enter` may commit multiline content;
- `Esc` cancels.

Save through the existing independent Trader Memo version/concurrency contract.

On conflict:

- do not silently overwrite;
- preserve the user's attempted value where practical;
- show a clear conflict/update indication.

Do not show Sales Memo in the Trader Grid.

---

## 17. Quote Confirmation

`Confirm` is a primary Trader action and remains visible in the toolbar.

Confirmation is required.

Do not confirm immediately from the toolbar without a review surface.

### Eligibility

The confirmation summary contains **only rows that are currently confirmable**.

Examples of rows that are not shown in the summary:

- not owned by the current Trader;
- not in Requested state;
- missing/invalid Working Quote;
- Manual quote missing required values;
- otherwise not eligible under current authorization/state.

Do not show selected-but-ineligible rows merely to explain why they are skipped.

The backend still revalidates every submitted item because eligibility can race.

If no selected row is currently confirmable, disable `Confirm`.

### Single and multiple selection

Use the same confirmation surface for one or many Quotes.

One selected confirmable row simply produces a one-row summary.

Multiple rows use the existing bulk Confirm Quote backend endpoint.

Do not fake bulk confirmation with N browser requests.

---

## 18. Quote Confirmation summary table

The confirmation modal contains a compact table, not a vertical list of cards.

Use decision-relevant columns only.

Reasonable initial columns:

```text
Case
Client
Security
Notl
Mode
Px
SY / Final SY
YSC
Slide
Expiry
```

Do not attempt to display every calculated field.

The confirmation summary is its own Grid/configuration surface.

It has an independent Grid config from the main Trader Grid.

The user may explicitly save:

- visible columns;
- order;
- widths;
- pinning where appropriate.

Do not autosave.

---

## 19. Confirm result behavior

The confirmation modal is a **pre-execution** review.

After Apply:

```text
confirmation modal closes
-> operation executes/completes
-> result is reported outside the modal
```

Do not replace the modal contents with a result list that requires another Close step.

For bulk Confirm, use the bottom Result Bar.

A successful single Confirm may use the same result mechanism or a lightweight success indication, but must not add another blocking modal.

---

## 20. Operations tab

The right pane `Operations` tab contains contextual operations for the current/selected RFQ(s).

This is the home for operations that should not crowd the top toolbar.

Candidate operations include, according to eligibility:

### Ownership / routing

```text
Pick Up
Release
Assign
Take Over
```

### Quote

```text
Mode
Expiry
Withdraw
```

### Contact Owner / lifecycle

When the current Trader is also the Contact Owner:

```text
Present
Unpresent
Hit
Away
Cancel
Reopen
Outcome correction
Change Contact Owner
```

Do not duplicate operations that are not authorized merely because the user has Trader role.

The existing authorization layer remains authoritative.

### Confirmation behavior

Use the agreed distinction:

No extra confirmation for routine operations such as:

```text
Pick Up
Release
Assign
Withdraw
Present
Unpresent
```

Keep explicit confirmation for stronger Trader-side operations such as:

```text
Take Over
Hit
Away
Cancel
Outcome correction
```

`Confirm Quote` has its separate summary confirmation flow and does not live only inside Operations.

Assign does not require an extra confirmation modal.

---

## 21. Bulk operations inside Operations

Keep backend-supported Trader bulk semantics.

Bulk-capable actions may be exposed contextually from Operations when multiple rows are selected.

Do not create a separate transient toolbar that replaces the screen based on selection count.

Use the real backend bulk endpoints for supported actions, including as applicable:

```text
Confirm Quote
Withdraw
Pick Up
Release
Assign Trader
Away
Cancel
```

Do not add Bulk Hit.

Take Over remains single-item.

The dedicated `Pick (N)` command is a special selection-independent bulk Pick Up for Assigned-to-me/Unowned RFQs.

---

## 22. Result Bar

Add a compact bottom Result Bar.

Normal collapsed form:

```text
Bulk Confirm: 9 ok / 2 skipped / 1 failed    [Details]
```

or:

```text
Pick: 12 ok / 1 skipped
```

The bar represents the most recent bulk operation result.

`Details` expands upward.

Useful detail columns:

```text
Case
Result
Code
Message
Log ID (when available)
```

Successful rows may be omitted from expanded details when all useful information is already conveyed by the summary.

Do not open a result modal.

The next bulk operation replaces the previous result.

---

## 23. Operations / Pricer pane behavior

The right pane has exactly:

```text
Operations | Pricer
```

Only one tab is visible at a time.

Toolbar behavior:

- `Ops` opens/selects Operations.
- `Pricer` opens/selects Pricer.
- if the pane is already open, switching between the buttons switches the tab.
- collapsing the pane does not destroy current Pricer state.

Do not create separate simultaneous panes for Operations and Pricer.

---

## 24. Pricer — role

Pricer is an independent scratch pricing tool.

It is not automatically synchronized with the currently selected RFQ after load.

The Pricer may start from:

- selected RFQ values;
- empty/manual scratch input.

Once loaded, Grid selection changes do not silently rewrite Pricer inputs.

Do not automatically track the selected RFQ.

---

## 25. Pricer — load behavior

Provide:

```text
Load Selected RFQ
```

When pressed:

- overwrite the current Pricer inputs immediately;
- do not show a confirmation dialog;
- set the Pricer source provenance to that RFQ.

The user explicitly chose Load, so confirmation adds unnecessary friction.

The initial fields loaded should include the relevant RFQ context such as:

```text
Security
Notional
Settlement Date
current/appropriate Calc Type
parameter
Slide
```

Where a value is not available, use the normal Pricer default.

---

## 26. Pricer provenance

When loaded from a Case, store explicit provenance such as:

```text
SourceCaseId
source Security
source Notional
source Settlement
```

The provenance determines whether Pricer output may be applied back to the RFQ.

Changing pricing-method inputs does **not** break provenance:

```text
Calc Type
Calculation parameter
Slide
```

Changing RFQ identity/terms does break provenance:

```text
Security
Notional
Settlement Date
```

When provenance is broken:

- clear/mark `SourceCaseId` as detached;
- disable Apply.

`Clear` also removes provenance.

If the source RFQ changes remotely in a way that makes the stored source terms stale, Apply must not silently write the old scratch result back to the changed Case.

Compare the current source Case terms before Apply and disable/fail safely when the source no longer matches.

---

## 27. Pricer Apply

Pricer output may be applied back to the originating RFQ only for **Manual Working Quote** workflow.

Button label should make the target explicit:

```text
Apply to #123
```

Apply target is the original `SourceCaseId`.

It is **not** the currently selected Grid row.

Changing Grid selection while Pricer remains open must not redirect Apply.

Apply writes the agreed Manual Working Quote values, at least:

```text
Px
Final SY
```

Apply does **not** Confirm the Quote.

Calculated-mode Apply is not supported.

If the source Case is not currently in Manual mode / no longer eligible, disable Apply or return a clear actionable error.

Use existing Working Quote update/concurrency semantics rather than bypassing Domain/Application use cases.

---

## 28. Pricer state lifetime

Closing/collapsing the right pane must not discard current Pricer inputs/results.

Keep state for the lifetime of the Trader workspace/component session.

No browser-reload persistence is required in this phase.

Do not persist scratch Pricer contents to server Grid config.

---

## 29. Pricer contents

Minimum Pricer controls:

```text
Security
Notional
Settlement Date
Calc Type
Calculation Parameter
Slide
Calculate
Results
```

Results should expose the useful desk metrics rather than raw JSON.

At minimum align result presentation with the Active Grid quote family.

Do not render `JSON.stringify` as the final Pricer UX.

Reuse the typed calculation boundary.

---

## 30. RFQ Search — placement

Add RFQ Search below the Active Grid.

This is a reference/investigation surface, not the primary workflow.

Use:

- draggable horizontal splitter between Active and Search;
- Search area collapsible;
- left filter panel collapsible;
- result Grid on the right.

Do not open a separate route/page for ordinary Trader search.

---

## 31. RFQ Search — execution

Search is explicit.

Do not execute a server search for every filter keystroke.

Use a visible `Search` command.

Pressing Enter while focus is in the Search filter inputs may execute Search.

Case ID should be easy to enter directly.

Use the existing:

```text
GET /api/rfqs/search
```

and preserve its server-side result cap / narrowing behavior.

---

## 32. RFQ Search — date presets

Historical date selection is preset-only.

Required presets:

```text
1M
3M
6M
1Y
2Y
5Y
```

Default:

```text
1Y
```

Do not add:

```text
Today
Custom
All
```

Map the preset to the existing `createdFrom` / `createdTo` semantics based on desk-local date behavior.

The backend remains `CreatedAt`-based.

---

## 33. RFQ Search — filters

Initial filters:

```text
Case
Date preset
Client
Security
Category
Contact Owner
Sales
Assigned Trader
RFQ Status
```

Use the existing search semantics.

Do not create an unnecessary free-form advanced query language.

---

## 34. RFQ Search result Grid

One Case = one result row.

Search result Grid is read-only in this phase.

Useful initial columns:

```text
Case
Date
Client
Security
Notl
Contact Owner
Trader
Status
Px
SY
YSC
```

If quote-summary fields are not currently present in `RfqSearchItem`, minimally extend the search projection so the result can show the agreed useful quote summary.

Do not route the user through Revision History merely to see the current/final useful quote values.

Do not implement row-expansion history in this phase.

---

## 35. Search history/detail scope

Do not add a generic Case Details Drawer in this phase.

Do not add Revision/Quote History UI merely because the APIs already exist.

The initial Search goal is:

```text
find the Case
see its useful current/final summary
communicate/reference the Case
```

Revision History / Quote History UI is explicitly lower priority and may be added later.

---

## 36. Search keyboard navigation

Trader shortcuts:

```text
Alt+S -> Search
Alt+A -> Active
```

### Alt+S

- expand Search if collapsed;
- focus the first useful Search control, preferably Case/Search input.

### Alt+A

- return focus to the Active RFQ Grid.

When focus is in Search:

- Trader quote-confirm shortcuts must not accidentally act on Search rows;
- Search is read-only;
- normal Search Enter behavior is preferred.

---

## 37. Quote Confirm shortcut

Use:

```text
Alt+Enter -> Confirm
```

Only when the Active Trader Grid context is active and it is safe to invoke the Quote Confirmation summary.

Do not let `Alt+Enter` confirm a Quote while the user is typing in Search, Memo, Pricer, or another text editor.

The shortcut opens the same confirmation summary as the toolbar `Confirm`.

Do not implement a separate shortcut-specific business flow.

---

## 38. Shortcut set

Keep the Trader shortcut set intentionally small.

Required:

```text
Esc
Alt+L
Alt+S
Alt+A
Alt+Enter
```

Semantics:

```text
Esc       -> cancel/close current transient editor/dialog where appropriate
Alt+L     -> toggle Live / Paused
Alt+S     -> jump to Search
Alt+A     -> jump to Active Grid
Alt+Enter -> Quote Confirmation
```

Do not add initial shortcuts for:

```text
Pick Up
Release
Assign
Take Over
Operations tab
Pricer tab
Withdraw
Hit
Away
Cancel
```

Those already have visible mouse paths.

Do not overload the keyboard because an operation exists.

---

## 39. Live / Pause refresh mode

Use the same high-level refresh concept intended for the Sales follow-up:

```text
Live
Paused
```

Default:

```text
Live
```

### Live

- remote-change/SSE notifications may trigger latest-snapshot refetch;
- do not replace active transient edits/calculations underneath the user.

If a Trader cell edit or calculation is active:

- defer automatic refresh application;
- remember that a refresh is pending;
- after the protected interaction ends, perform one catch-up refresh.

Do not replay individual remote events into business state.

### Paused

- keep the visible snapshot stable;
- continue receiving remote-change notifications;
- track/display pending updates;
- do not automatically apply them.

Unlike Sales, Paused currently does **not** change Trader action eligibility.

Its purpose on Trader is visual/workspace stability.

---

## 40. Paused-mode mutation reconciliation

Paused mode is a **stable visible snapshot** mode.

Successful mutations while Paused must not trigger a full Trader-list refresh merely because the operation succeeded.

Preferred behavior:

```text
Live
  -> operation succeeds
  -> normal latest-snapshot refresh is allowed

Paused
  -> operation succeeds
  -> do not replace the whole visible snapshot
  -> patch only the affected row when the API response contains authoritative post-operation state
```

Examples of authoritative responses include:

- Working Quote result;
- ownership result;
- lifecycle result;
- Contact Owner result;
- Trader Memo result;
- Quote Confirm result.

Use the returned server state rather than recreating Domain transitions in React.

If the API response does **not** contain enough information to represent the new row state correctly:

- do not invent or infer the missing Domain transition in the frontend;
- keep the existing visible row stable;
- mark the screen/Case as requiring reconciliation where useful;
- reconcile on explicit `Refresh` or `Paused -> Live`.

The frontend must not construct impossible combinations such as a lifecycle state and quote state that cannot coexist in the Domain.

This rule applies to Trader operations generally, including Operations-pane actions, Working Quote operations, Memo changes, Contact Owner changes, and lifecycle actions.

Calculation is slightly different because its normal successful API response already contains the authoritative Working Quote result. Patch that row from the returned result while preserving the Paused snapshot.

Manual Refresh and Resume remain the full-snapshot reconciliation boundaries.

---

## 42. Resume / manual Refresh

Paused -> Live:

1. refetch latest snapshot once;
2. reevaluate sort/filter;
3. clear pending-update indication;
4. resume normal Live behavior.

Manual Refresh:

- works in Live or Paused;
- fetches the latest snapshot immediately;
- while Paused, remain Paused after the fetch.

Manual Refresh is an explicit escape hatch.

If a local calculation wait has become stuck/stale, manual Refresh may clear the local wait state; stale responses must then be ignored.

---

## 42. Do not silently move rows during normal work

Avoid surprising row movement while the Trader is operating.

In particular:

- do not introduce an automatic `Needs Attention` sort in this phase;
- do not overwrite user sort;
- do not use state transitions to force rows to another position.

A full explicit Refresh or Resume may naturally reevaluate current Grid sort/filter rules.

This is acceptable because the user explicitly requested/re-enabled reconciliation.

---

## 43. Grid configurations

Trader has three independent Grid configuration surfaces:

```text
Trader Main
Trader Search
Quote Confirmation
```

Use separate server config keys.

For example:

```text
screenId = trader
configKey = main

screenId = trader
configKey = search

screenId = trader
configKey = confirm
```

Exact key names may differ but must remain stable.

Persist:

- visibility;
- order;
- width;
- pinning;
- other existing safe layout state.

Do not autosave.

Use explicit Save/Reset.

These controls may live in the Grid context menu to avoid toolbar clutter.

If the API returns invalid/incompatible config:

- fall back to the built-in default;
- keep the screen usable;
- do not automatically overwrite the server value;
- the next explicit Save replaces it.

Do not persist:

- current row selection;
- Live/Pause state;
- Search result data;
- Pricer scratch state;
- Result Bar contents.

---

## 44. Search and confirmation config independence

Do not reuse Main Grid config for Search or Quote Confirmation merely because they use AG Grid.

Each surface has different information density and column priorities.

Changing Search columns must not mutate Main Grid layout.

Changing Quote Confirmation summary columns must not mutate Main Grid layout.

---

## 45. Current quote expiry behavior

Preserve the existing typed Quote Expiry semantics:

```text
None
After(TimeSpan)
```

Do not reintroduce nullable implicit business meaning.

The Trader UX may keep expiry in Operations / confirmation context rather than occupying permanent top-toolbar width.

The Quote Confirmation summary must show the effective expiry being confirmed.

Do not change expiry semantics as part of this UX phase.

---

## 46. Existing Domain semantics to preserve

Do not regress:

- ownership semantics;
- Assigned Trader vs runtime Ownership distinction;
- Take Over semantics;
- Contact Owner authorization;
- Requested vs Quoted state;
- Presented protects against Withdraw;
- Quote Confirm does not recalculate;
- Working Quote calculated/manual payload retention;
- Quote expiry semantics;
- Working Quote optimistic concurrency;
- RFQ optimistic concurrency;
- Trader Memo independent concurrency;
- Bulk per-item independent transaction semantics;
- no Bulk Hit.

The UI may combine fields visually but must not flatten the Domain meaning.

---

## 47. Calculation error API

The current Application already creates a persistent calculation failure record.

Ensure the HTTP error contract exposes enough structured information for Trader support workflow.

For Calculation Failure (`422`), include at least:

```text
code = CalculationFailure
detail/message
calculation error code
failureLogId
correlation/trace id where available
```

Do not replace the standard error taxonomy.

Add structured extensions to the existing ProblemDetails/error contract rather than inventing a second error envelope only for Trader.

---

## 48. Calculation model extension discipline

The current calculation payload/driver set is narrower than the target Trader UX.

Extend it deliberately.

Do not add display-only untyped dictionaries.

Prefer typed Domain/Application/API fields for the agreed calculation metrics.

At minimum, YSC must be a first-class supported Trader quote metric and calculation driver if the desk calculation boundary supports it.

The agreed spread family should be represented consistently across:

- Working Quote calculation payload;
- API response;
- Trader read model;
- Pricer result;
- confirmation summary where selected;
- Grid formatting.

If the development calculator is synthetic, deterministic mock behavior is acceptable for wiring/tests.

Do not implement or embed production financial models solely for this frontend phase.

---

## 49. Current backend/API assets to reuse

Reuse existing endpoints/use cases where possible:

- Trader active RFQ query;
- Pick Up / Release / Assign / Take Over;
- bulk Pick Up / Release / Assign;
- Calculate Working Quote;
- Change Working Quote Mode;
- Update Manual Working Quote;
- Confirm Quote;
- Bulk Confirm Quote;
- Withdraw / Bulk Withdraw;
- Away / Bulk Away;
- Cancel / Bulk Cancel;
- Contact Owner changes;
- Trader Memo;
- RFQ Search;
- Grid Config;
- Quote Expiry;
- Scratch Pricer;
- SSE/latest-snapshot refresh infrastructure.

Do not create duplicate Trader-only endpoints merely because the UI is being reorganized.

---

## 50. Minimal new backend/API work expected

This phase may require focused additions including:

1. Trader read model:
   - `SalesAndTradingMessage`
   - `StateSince`
   - additional quote metrics required by the agreed Grid.

2. Calculation contract:
   - YSC;
   - agreed spread-family values/drivers needed by Trader;
   - structured failure details at HTTP boundary.

3. RFQ Search projection:
   - compact quote summary required by Trader Search results.

4. User settings:
   - `Default Quote Mode`.

Do not broaden these additions into unrelated architecture refactoring.

Regenerate OpenAPI artifacts through the repository's normal generation process when contracts change.

Do not manually edit generated schema files.

---

## 51. Frontend state / command discipline

Do not scatter the same operation implementation across:

- toolbar;
- Operations pane;
- context menu;
- shortcut.

Multiple invocation surfaces should call one feature-local operation/action function.

A Trader-local hook/helper/reducer is acceptable where it reduces callback duplication.

Do not create a generic application-wide command bus.

Do not copy RTK Query server state into a second global Redux store.

Keep server state in the existing data-query layer.

---

## 52. Visual constraints

The finished Trader screen should feel like a dense front-office blotter.

Prefer:

- stable layout;
- compact labels;
- grouped columns;
- high information density;
- low-latency inline editing;
- restrained dark-theme styling;
- small badges/icons;
- visible Case IDs;
- clear attention hierarchy.

Avoid:

- large cards;
- large explanatory banners;
- permanent oversized forms;
- selection checkboxes as the main interaction;
- bright full-row state rainbow coloring;
- modal result screens;
- automatic row jumping;
- duplicated Operations and Pricer panes;
- raw JSON Pricer output;
- giant generic details panels.

Keep the existing global dark mode.

---

## 53. Tests — Trader screen

Add/update focused frontend tests for at least:

### Layout

- Active Grid + Search split;
- Search collapse/expand;
- right pane `Operations | Pricer`;
- tabs are mutually exclusive;
- pane state preserves Pricer contents while collapsed.

### Selection

- Excel-like multi-row selection configuration;
- no checkbox-dependent UX;
- selected-row left marker;
- attention background remains independent from selection.

### Attention

- Assigned-to-me + Unowned receives strongest attention;
- Owned-by-me + Requested receives secondary attention;
- Quoted/Presented are not strongly colored merely because of state;
- no automatic attention sort.

### Quote edit

- explicit edit start, not single-click;
- driver cell chooses correct CalculationDriver;
- Slide retains current driver;
- one calculation in flight per Case;
- other rows remain editable/usable;
- failure reverts/preserves previous Working Quote;
- stale calculation response does not overwrite refreshed state.

### Calc failure

- calculating indicator;
- failure indicator;
- failure details expose error code and failureLogId when present.

### Manual

- Calculated -> Manual presentation;
- only Manual Px / Final SY editable as agreed;
- returning to Calculated restores retained calculated values.

### Memo

- Trader Memo inline edit;
- Sales Memo is not shown;
- version conflict does not silently overwrite.

### Confirm

- summary includes only confirmable selected rows;
- one-row summary works;
- bulk uses bulk-confirm API;
- ineligible selected rows are not included;
- confirmation table config independent from Main;
- modal closes after Apply;
- result appears outside modal.

### Pick Assigned

- `Pick (N)` count;
- selection-independent targeting;
- bulk Pick endpoint;
- no confirmation modal;
- partial failure Result Bar.

### Operations

- contextual eligibility;
- Assign without confirmation;
- Take Over confirmation;
- Contact Owner-only lifecycle actions;
- no Bulk Hit.

### Pricer

- Load Selected overwrites without confirmation;
- later Grid selection does not auto-track;
- changing Security/Notional/Settle breaks provenance;
- changing driver/parameter/Slide preserves provenance;
- Clear breaks provenance;
- Apply target is SourceCaseId, not current selection;
- Apply only for Manual Working Quote;
- Apply does not Confirm;
- remote source-term change invalidates safe Apply.

### Search

- preset-only date range;
- default 1Y;
- 1M/3M/6M/1Y/2Y/5Y;
- no Today/Custom/All;
- explicit Search;
- Search is read-only;
- Alt+S / Alt+A behavior;
- Trader Confirm shortcut does not act from Search.

### Refresh

- Live normal refresh;
- active edit/calculation defers remote refresh;
- catch-up refresh after protected interaction;
- Paused holds current snapshot;
- pending update indication;
- Alt+L;
- manual Refresh while Paused remains Paused;
- stale calc response ignored after refresh.

### Grid config

- independent main/search/confirm configs;
- explicit Save;
- Reset;
- invalid server config falls back to default;
- invalid config is not automatically overwritten.

---

## 54. Backend/API tests

Add focused tests for any contract changes introduced by this phase.

At minimum where applicable:

- Trader `SalesAndTradingMessage`;
- Trader `StateSince`;
- YSC/quote metric serialization;
- calculation driver mapping;
- calculation failure HTTP payload contains failureLogId;
- Default Quote Mode get/save;
- Working Quote creation uses Assigned Trader default setting and falls back to Calculated;
- RFQ Search quote summary;
- Search result cap remains unchanged;
- desk-local date behavior remains correct;
- bulk Confirm and bulk Pick behavior remains existing per-item semantics.

Do not weaken existing Domain/Application tests.

---

## 55. Validation

Run at minimum:

```bash
dotnet test

cd src/Rfq.Web
npm test -- --run
npm run build
```

Regenerate/validate OpenAPI artifacts if API contracts change.

Smoke-test `/trader` with at least:

- Assigned-to-me Unowned RFQ;
- Owned Requested RFQ;
- Calculated edit;
- Slide edit;
- calculation failure;
- Manual mode;
- Trader Memo;
- single Quote Confirm;
- multi Quote Confirm;
- Pick (N);
- Operations tab;
- Contact Owner lifecycle operation;
- Pricer from RFQ;
- Pricer detached by term change;
- Pricer Apply to Manual quote;
- Search default 1Y;
- Search 5Y;
- Live remote update;
- Paused remote update;
- manual Refresh;
- Grid layout Save/Reset.

Fix regressions introduced by the Trader UX redesign.

---

## 56. Commit discipline

Keep this as a reviewable Trader UX phase.

A reasonable commit sequence is:

```text
1. minimal Trader read-model / settings / calculation-contract additions
2. Trader main Grid + selection + attention + inline editing
3. Operations / Confirm summary / Result Bar
4. Pricer pane + provenance / Apply
5. RFQ Search split pane
6. Live/Pause + shortcuts + Grid configs
7. tests / cleanup
```

The exact number of commits may vary.

Do not mix unrelated Daily Review redesign.

If the separate Sales follow-up work is also in progress, keep Trader commits independently reviewable and avoid silently rewriting Sales behavior as part of this file.

---

## 57. Final implementation response

The implementation response should contain:

1. final Trader screen structure;
2. Trader read-model/API additions;
3. Active Grid attention/selection behavior;
4. quote edit / calculation behavior;
5. Quote Confirmation behavior;
6. Operations behavior;
7. Pricer provenance / Apply behavior;
8. Search behavior;
9. Live/Pause behavior;
10. Grid config behavior;
11. tests/build executed and results;
12. commit SHA(s);
13. any deviation from this instruction and why.

Do not continue into Daily Review or history redesign after completing this phase.
