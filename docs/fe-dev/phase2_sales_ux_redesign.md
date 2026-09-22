# Phase 2 — Sales Workspace UX Redesign

## Goal

Redesign the Sales workspace around the operating model agreed after Phase 1.

Baseline:

- repository: `sugaim/rfqfin`
- baseline commit: `fb5a54417bef73659010599107c9620adb3c3108`
- frontend: `src/Rfq.Web`
- Sales feature:
  - `src/Rfq.Web/src/features/sales/SalesWorkspace.tsx`
  - `src/Rfq.Web/src/features/sales/SalesScreen.tsx`

Phase 1 already established URL routing, feature-local workspaces, global dark theme, and the current backend/API behavior.

Phase 2 is primarily a **Sales UX redesign**. It may make the **minimal Sales read-model/API extensions required by that UX**, but it must not redesign Domain lifecycle semantics or unrelated Trader / Daily Review workflows.

The target Sales screen is a high-density RFQ blotter with:

1. a global toolbar;
2. a large AG Grid RFQ list;
3. one stable Work Pane for the currently selected RFQ / New RFQ / bulk selection;
4. a lightweight AG Grid status bar for selection and shortcut hints;
5. a right-side `Recent Revisions` drawer.

Do not add a permanent lower work area.

---

## 1. Scope and non-goals

Implement the agreed Sales workspace UX.

Do not redesign in this phase:

- Trader workspace;
- Daily Review workflow;
- Booking integration;
- post-trade / booking completion tracking;
- Operation Log;
- historical RFQ Search as a new Sales sub-page;
- a generic RFQ detail page;
- generic shared UI abstractions;
- API code generation architecture;
- authentication / authorization model;
- Domain lifecycle/state semantics;
- new bulk Hit support.

Do not create speculative generic components solely because later screens may look similar.

Keep Sales-specific behavior under `features/sales/`.

---

## 2. Overall screen structure

Target structure:

```text
+-------------------------------------------------------------------+
| Toolbar                                                           |
+-----------------------------------------------+-------------------+
|                                               |                   |
|                  RFQ Grid                     |     Work Pane     |
|                                               |                   |
|                                               |                   |
+-----------------------------------------------+-------------------+
| AG Grid Status Bar: selection + shortcut hints                    |
+-------------------------------------------------------------------+

                                      Recent Revisions -> right Drawer
```

Initial implementation:

- place the Work Pane on the **right**;
- structure the layout so moving it to the left later is a trivial layout/CSS change;
- do not couple business behavior to left/right position;
- do not add a user preference for left/right position yet.

The Grid is the main working surface. The Work Pane is the stable operation surface.

Do not create:

- a generic bulk contextual action bar above/below the Grid;
- an amendment action strip below the Grid;
- a permanent Recent Events area;
- a permanent Operation Log area.

---

## 3. Toolbar

Keep the toolbar compact.

Required controls:

```text
[New RFQ] [Filter: All RFQs v] ... [Recent Revisions] [Save Layout] [Reset Layout]
```

The toolbar is for global screen actions only.

Do not put normal row lifecycle operations such as Hit / Away / Present / Cancel in the toolbar.

Those belong to:

- Work Pane;
- row context menu;
- keyboard shortcuts.

---

## 4. Sales preset Filter

Use one one-dimensional preset filter control.

UI label:

```text
Filter
```

Values:

```text
All RFQs
Owner = Me
Sales = Me
Owner & Sales = Me
Owner | Sales = Me
```

Do not model these as two independent toggles.

The normal AG Grid column filters remain independent and may be combined with this preset.

### Important current-backend constraint

The current `GET /api/sales-rfqs` read model is already scoped to:

```text
SalesId == current user
OR
ContactOwnerId == current user
```

Therefore, with the current server visibility scope, `All RFQs` and `Owner | Sales = Me` are currently equivalent.

Do **not** silently broaden authorization/visibility merely to make these presets distinct.

For Phase 2:

- preserve current server-side Sales visibility unless a separately justified visibility change is required;
- still implement the agreed preset semantics against the data available to the Sales screen;
- add `salesId` to the Sales read model because the frontend currently cannot distinguish `Sales = Me`;
- keep the filtering implementation isolated enough that a later desk-wide Sales projection can make all five presets distinct without UI redesign.

Do not persist this preset as part of Grid layout in this phase.

---

## 5. Selection and mode model

Do not store an arbitrary Work Pane mode independently of RFQ state.

Derive the pane from:

- explicit New-RFQ intent;
- current selection;
- server RFQ state;
- number of selected rows.

Conceptual modes:

```text
No selection       -> Neutral
explicit New       -> New
Draft row          -> Draft Edit
Active/Requested   -> Waiting Quote
Active/Quoted      -> Quote Returned
Presented          -> Presented
Cancelled          -> Cancelled
Hit                -> Hit
Away               -> Away
2+ selected rows   -> Bulk
```

A pending amendment is **not** a separate lifecycle mode.

It is a modifier of the selected RFQ:

- current confirmed RFQ remains authoritative;
- pending amendment is shown as unconfirmed Sales work;
- Work Pane may show an amendment diff/actions in addition to the lifecycle content.

Use stable row identity:

```text
getRowId = caseId
```

Prefer storing selected `caseId` and deriving the selected row from current query data rather than retaining a stale copied row object across refreshes.

When refresh updates row data:

- preserve selection if that Case still exists;
- clear selection if the selected Case disappears.

`No selection` must not implicitly mean New RFQ.

New RFQ starts only from explicit `[New RFQ]`.

---

## 6. Work Pane — general rules

The Work Pane answers:

> What should Sales do next for this RFQ?

It is not a raw object dump.

Use:

- compact typography;
- low vertical whitespace;
- one stable pane width;
- a thin/low-saturation state accent rather than a full-pane state-colored background.

Common information should include only what is useful for Sales judgment:

- Case / state;
- Client;
- Security;
- Notional;
- Settlement;
- Trader;
- relevant confirmed Quote summary;
- Sales & Trading Message;
- Sales Memo.

Memo should normally be compact:

```text
Memo: one-line preview   [Edit]
```

Expand to a small editor only while editing.

Do not make the memo a permanently large textarea.

---

## 7. Work Pane — New / Draft

New and saved initial Draft use the same basic vertical form.

Use one predictable top-to-bottom field column:

```text
Client
Security
  Category
  Trader
Settle
Notl
Message
```

### Client

- canonical selection is required;
- show primary display name;
- show secondary identifier in a lighter/smaller form where useful;
- autocomplete result rows should be one line and dense.

### Security

- canonical selection is required;
- show primary security display;
- show secondary identifier/display beneath where useful;
- Category and Trader belong visually under Security because they are resolved/routed from Security context;
- autocomplete candidate rows should be one line and dense.

### Settlement / Notional

Order must be:

```text
Settle
Notl
```

Compact labels such as `Settle` and `Notl` are acceptable.

### Message

Use a compact input; do not reserve excessive height by default.

### Keyboard behavior

- normal form progression uses `Tab`;
- Enter inside autocomplete selects the highlighted candidate;
- do not implement a global Enter-to-jump-to-next-field behavior.

### Draft validation

Saving Draft requires at least:

```text
Client
Security
```

Confirm requires the full backend-required confirmed-RFQ input.

Do not unnecessarily impose Confirm validation on Save Draft.

---

## 8. Creation defaults

Creation defaults run from logical Security selection/change during **initial New/Draft creation**.

When SecurityId changes before initial confirmation:

- resolve creation context;
- reset Category to the security/default category;
- reset Assigned Trader from routing;
- recompute Standard Settlement;
- reset actual Settlement to Standard Settlement.

Do not add Auto/Manual provenance persistence.

Do not reapply defaults merely because an existing Draft is loaded.

Do not call creation defaults on Reopen.

Once an initial RFQ has been confirmed:

- Client cannot be amended;
- Security cannot be amended.

Changing Client or Security means a new Case, not an Amendment Revision.

---

## 9. Work Pane — Waiting Quote

For:

```text
Active + Requested
```

show:

- identity/terms;
- Trader;
- request reason;
- elapsed time since the displayed workflow state began;
- Message;
- Memo.

Do not force a fake primary operation simply because the pane normally has actions.

Available lifecycle operation:

```text
Cancel
```

Amendment editing is primarily done in the Grid and is also accessible from context menu / Work Pane if a pending amendment exists.

---

## 10. Work Pane — Quote Returned

For:

```text
Active + Quoted
```

show the current **confirmed Quote** prominently.

Sales-facing Quote values should be decision-relevant, not a pricing-engine debug dump.

Show, where available:

```text
Price
Yield
Simple
G-Spread
```

Use a compact 2–3-column arrangement rather than a tall form.

Primary normal-flow action:

```text
Present
```

Also expose:

```text
Hit
Away
```

because presentation is not required before closing Hit/Away under the existing domain rules.

---

## 11. Work Pane — Presented

For `Presented`, show the presented confirmed Quote and RFQ terms.

Main actions:

```text
Hit
Away
```

Secondary action:

```text
Unpresent
```

Do not make Unpresent visually compete with Hit/Away.

---

## 12. Work Pane — Cancelled

Show the final/current RFQ terms and relevant quote context.

Main action:

```text
Reopen
```

A pending amendment may still exist because Cancel preserves it.

If one exists, show the pending amendment state/diff as an additional modifier.

---

## 13. Work Pane — Hit / Away

These are terminal lifecycle states.

### Hit

Show:

- executed/closed confirmed Quote snapshot;
- RFQ terms;
- Memo.

Do not implement Booking workflow in Phase 2.

Do not invent a `Booking Pending` RFQ lifecycle state.

Outcome correction may be available as a low-emphasis/overflow operation.

### Away

Show:

- final relevant Quote/terms;
- Memo.

No strong normal action is required.

Outcome correction may be available as a low-emphasis/overflow operation.

---

## 14. Amendment UX

Preserve the existing business semantics:

- Revision-owned editable fields:
  - Notional;
  - Settlement Date;
  - Sales & Trading Message.
- editing a confirmed RFQ creates/updates one pending Draft Revision;
- cell commit autosaves/upserts the pending Amendment;
- current confirmed RFQ/Quote state does not change until Amendment Confirm;
- Trader does not see pending Draft contents;
- Amendment Confirm supersedes the prior confirmed Revision and requests a Revised quote;
- Amendment Discard leaves the current confirmed Revision/Quote/state unchanged.

### Grid behavior

Inline edit remains the primary fast editing path.

When editing a confirmed RFQ row:

- automatically make that row the active/selected RFQ;
- persist the cell commit through the existing Amendment upsert;
- visibly mark fields that differ from the current confirmed Revision.

Use:

- subtle Draft/Amend family row treatment where appropriate;
- stronger/local changed-cell highlight;
- `AMEND` badge or similarly compact marker.

Do not add the previously considered Amendment strip below the Grid.

### Work Pane

If the selected Case has a pending Amendment:

- show a compact current-vs-draft diff;
- show `Confirm Amendment`;
- show `Discard Amendment`.

Single-item Confirm/Discard are immediate.

No confirmation dialog for single Amendment Confirm/Discard.

### Shortcut

For a single selected pending Amendment:

```text
Alt+Enter
```

confirms immediately.

For multiple selected pending Amendments:

```text
Alt+Enter
```

opens the required bulk confirmation modal.

---

## 15. Grid density and visual hierarchy

Use a high-density blotter layout.

Prefer:

- smaller row height;
- compact headers;
- tight column widths;
- ellipsis/clipping where appropriate;
- tooltips where useful.

Do not make every Client/Security value fully visible by default.

Client and Security are the primary visual identifiers and should be the left-pinned Grid anchors.

Do **not** pin Case ID ahead of them.

---

## 16. Grid column organization

Use AG Grid column groups aggressively.

### Identity

Pinned compact anchors:

```text
Client
Security
```

These remain visible when their detail groups expand.

### Client group

Closed/default:

```text
Client
```

Open:

```text
Client
Client Name / detail
Client ID
```

Duplicate display/detail columns are acceptable if they make group expansion practical.

Use explicit stable `colId` / `groupId`.

### Security group

Closed/default:

```text
Security
```

Open:

```text
Security
Japanese Name
BBG Display
Security ID / other useful identifier
```

Keep the compact Security anchor pinned even when expanded.

### Terms

Default visible:

```text
Notl
Settle
Trader
```

### State group

Closed/default:

```text
State
```

Open:

```text
RFQ Status
Quote Status
Reason
```

The default display `State` is a UI-derived summary such as:

```text
DRAFT
WAITING
QUOTED
PRESENTED
CANCELLED
HIT
AWAY
```

Pending Amendment is not a replacement lifecycle State; show it separately as `AMEND`.

### Time group

Closed/default:

```text
Elapsed
```

Open:

```text
Created
State Since
Elapsed
```

`Elapsed` means duration since the current displayed workflow state began.

Do not use `CreatedAt` as a substitute for this.

### Quote group

Closed/default:

```text
Price
```

Open:

```text
Price
Yield
Simple
G-Spread
```

### Message

Keep `Message` as a normal column.

It may be moderately wide with ellipsis/tooltip.

### Case

Case ID is useful but secondary to Client/Security.

Place it later in the default order or allow it to be hidden through layout configuration.

---

## 17. Row / cell color semantics

Keep colors restrained and dark-theme friendly.

### Quote Returned

`Active + Quoted` uses a noticeable but low-saturation amber-family row treatment.

This is the strongest normal Sales attention state.

### Draft / Pending Amendment

Use a very subtle blue/purple-family work-in-progress treatment.

Draft and Amendment may share the family but must have distinct badges/labels.

### Requested / Presented

Normal background unless another modifier applies.

### Hit / Away / Cancelled

Dim/gray terminal treatment.

Do not paint an entire Hit row bright green or an entire Away row bright red.

### Cells

Use local cell highlighting for Amendment changes.

### Overlap priority

If a Case has a pending Amendment while its current confirmed RFQ is Quoted:

- retain the Quoted/amber row attention;
- show `AMEND` badge;
- retain changed-cell highlighting.

Do not combine multiple competing full-row backgrounds.

---

## 18. Current confirmed Quote data — required read-model extension

The current Sales RFQ response contains Quote IDs/status but not the confirmed Quote values required by the agreed Grid/Work Pane UX.

Extend the **Sales read model** with the current/closed confirmed Quote summary needed by Sales.

Do not expose Working Quote editing internals unnecessarily.

The Sales projection should be able to render at least, when available:

```text
Price
Yield / BBG Yield
Simple / final simple yield
G-Spread
```

For Manual confirmed quotes, expose only meaningful available values and render unavailable values blank.

Keep the projection Sales-specific.

Do not make Sales depend on the Trader screen DTO.

Do not force the frontend to issue one Quote-history request per Grid row.

Avoid N+1 requests.

---

## 19. SalesId and state timing — required read-model extensions

Add to the Sales RFQ read model the information required for the agreed UI:

```text
SalesId
StateSince
```

`SalesId` is required for the Filter preset.

`StateSince` must represent when the currently displayed workflow state began, sufficient to render:

```text
Elapsed
State Since
```

Do not fake this from `CreatedAt`.

If the exact server-side field name differs, keep the semantics above.

The frontend may compute elapsed display from `StateSince` and current time.

---

## 20. Layout persistence

Keep explicit layout persistence.

Controls:

```text
Save Layout
Reset Layout
```

Save the AG Grid column state required for:

- order;
- width;
- visibility;
- pinning;
- existing sort state if it is already part of the stored column state.

Do not persist normal Grid filter state as part of Layout.

`Reset Layout` restores the application-standard layout locally.

Do not add a navigation warning for unsaved Grid-layout changes.

Do not introduce multiple named layouts in Phase 2.

---

## 21. AG Grid Enterprise capabilities

This application is intended to use AG Grid Enterprise for the production Sales screen.

Phase 2 may add the matching `ag-grid-enterprise` package/version and register the Enterprise modules required by the agreed UX.

Use native AG Grid features where appropriate for:

- context menu;
- status bar;
- column tool panel / column grouping behavior;
- filtered selection behavior.

Do not write custom replacements solely to stay on Community if Enterprise already provides the required behavior.

Do not redesign licensing/deployment policy in this phase.

---

## 22. Row context menu

Add a row context menu.

Its purpose is to execute commands directly from the Grid without moving to the Work Pane.

Only show commands valid/relevant for the clicked row/current state.

### Lifecycle quick actions

As applicable:

```text
Present
Unpresent
Hit
Away
Cancel
Reopen
```

### Amendment

If a pending Amendment exists:

```text
Confirm Amendment
Discard Amendment
```

### Other row-level actions

Expose existing row-origin actions where they already have clear behavior, such as:

```text
Create New from Existing
Copy >
  Case ID
  Security ID
  Client ID
```

Do not invent a new Search/History destination solely to fill the context menu.

### Confirmation behavior from context menu

Immediate:

```text
Present
Unpresent
Reopen
Confirm Amendment
Discard Amendment
```

Require confirmation:

```text
Hit
Away
Cancel
Outcome correction
```

Confirmation must clearly identify the Case / Security.

---

## 23. Work Pane confirmation behavior

Work Pane operations are performed while the user is already looking at the selected RFQ and its state.

Therefore, **do not show a confirmation dialog for normal single-item Work Pane commands**.

This includes:

```text
Present
Unpresent
Reopen
Hit
Away
Cancel
Confirm Amendment
Discard Amendment
```

The backend optimistic version/concurrency check remains authoritative.

Outcome correction may remain a lower-emphasis exceptional operation.

Keep visually dangerous/destructive actions separated enough to reduce mis-click risk.

Booking/report side effects are not triggered in Phase 2.

---

## 24. Keyboard shortcuts

Use a consistent `Alt` modifier.

Initial mapping:

```text
Alt+N       New RFQ
Alt+P       Present
Alt+U       Unpresent
Alt+H       Hit
Alt+A       Away
Alt+C       Cancel
Alt+R       Reopen
Alt+Enter   Confirm / Apply
Esc         cancel/close the current transient UI where appropriate
```

Do not add a shortcut for exceptional outcome correction in Phase 2.

### Shortcut safety

When focus is inside an ordinary text/input/textarea/select/autocomplete editing surface:

- do not trigger character shortcuts unexpectedly;
- `Alt+Enter` may be handled by the active RFQ form/amendment workflow only when that meaning is explicit.

The command must also be valid for the current selected state.

### Shortcut confirmation

Immediate:

```text
Alt+P Present
Alt+U Unpresent
Alt+R Reopen
Alt+Enter single Amendment Confirm
```

Confirmation dialog:

```text
Alt+H Hit
Alt+A Away
Alt+C Cancel
```

For bulk operations, shortcuts always enter the same bulk confirmation modal used by mouse operation.

A shortcut must never bypass bulk confirmation.

---

## 25. Status Bar

Use the AG Grid Status Bar as a lightweight information/hint surface.

Do not place primary operation buttons in it.

Show context-sensitive information such as:

```text
3 selected
Alt+A Away
Alt+C Cancel
Right-click for actions
```

For a single pending Amendment:

```text
AMEND pending
Alt+Enter Confirm
Right-click for more
```

Only show shortcuts relevant to the current selection/state.

Keep this visually quiet.

---

## 26. Bulk UX

When multiple rows are selected:

```text
Work Pane -> Bulk mode
```

Do not add a separate generic bulk action strip.

The Bulk Work Pane should show:

- selected count;
- currently available bulk operations;
- enough summary to understand the target set.

Bulk actions are driven by the existing business/API bulk capabilities.

### Important: do not add Bulk Hit

The current domain/application design explicitly does **not** bulk-enable Hit.

Do not introduce Bulk Hit in Phase 2.

Use currently supported Sales bulk operations where appropriate, including operation-specific bulk APIs such as:

- Away;
- Cancel;
- Present / Unpresent where applicable;
- initial Draft Confirm / Discard where exposed in Sales;
- Amendment Confirm / Discard.

Do not simulate unsupported bulk operations by firing N single HTTP requests.

### Eligibility

A bulk operation may be offered when at least one selected row is eligible.

The confirmation modal must classify every selected target before Apply.

Example:

```text
✓ Case 101   Away
✕ Case 103   Quote not returned
! Case 105   Pending amendment / other relevant note
```

Use icons and concise reason text.

### Snapshot / concurrency

Opening the confirmation modal snapshots:

- target Case IDs;
- expected versions;
- operation input needed by the bulk command.

Apply must use that snapshot.

If a Case changes after confirmation opened:

- that item fails/skips according to backend concurrency/business semantics;
- do not silently re-evaluate the latest state and apply the command to a different version.

### Result

Render per-item:

```text
Succeeded
Skipped
Failed
```

and operator-readable messages.

After Apply, return to the normal Sales screen.

No persistent Operation Log in Phase 2.

---

## 27. Filtered Select All

Support:

```text
filter Grid
-> select all filtered rows
-> run bulk operation
```

Use AG Grid Enterprise selection behavior rather than a custom duplicated selection model.

If selection survives a filter change and includes rows not currently visible:

- make the selected count clear;
- the bulk confirmation modal is the final authoritative display of the actual target set.

Do not silently limit the operation to only currently visible rows after selection.

---

## 28. Auto refresh from SSE/events

Change the Sales UX from the current always-manual refresh behavior to:

```text
remote event / SSE wake-up
    -> not editing / not in confirmation
         -> automatically refetch Sales RFQ snapshot
    -> editing / confirmation open
         -> defer refresh
```

Protected transient states include at least:

- New/Draft form actively being edited;
- active cell/form edit whose local value must not be destroyed;
- bulk confirmation modal;
- single-operation confirmation modal from context menu/shortcut.

When the protected edit/transient state ends:

```text
pending remote update?
    -> refetch once
    -> catch up to latest snapshot
```

Do not replay every event into frontend state.

The authoritative UI state remains the latest Sales RFQ snapshot.

### App/SSE plumbing

The current App owns the SSE wake-up/event cursor.

Refactor only as much as needed so Sales can:

- observe that remote change exists;
- defer while protected;
- acknowledge/catch up after successful refetch.

A reasonable shape is to expose an acknowledge/cursor callback through the existing App outlet context rather than creating another EventSource in Sales.

Do not create duplicate SSE connections merely for the Sales feature.

Do not redesign Trader/Daily Review refresh behavior unnecessarily.

### Conflict during a mutation

If a mutation fails because the expected version is stale:

- do not automatically overwrite the user's in-progress local input;
- show a clear updated-elsewhere/conflict state;
- allow explicit review/reload.

Do not falsely report success.

---

## 29. No generic Recent Events panel

Do not add a permanent Sales `Recent Events` list.

SSE/events are primarily infrastructure for:

- wake-up;
- refresh/catch-up;
- audit/history support.

---

## 30. Recent Revisions Drawer

Add a toolbar action:

```text
Recent Revisions
```

Open a Drawer from the **right**.

This is a cross-Case, newest-first feed of **business-significant confirmed-to-confirmed changes**.

It is not:

- the raw SSE event feed;
- an Operation Log;
- a full case audit history.

### Include

Two kinds:

```text
RFQ Revision
Quote Revision
```

Only include a revision when there is a previously confirmed value to compare against.

Therefore:

- initial RFQ confirmation is not a `Recent Revision` item;
- first-ever Quote confirmation is not a `Recent Revision` item;
- a later confirmed RFQ Amendment is;
- a later confirmed Quote replacing/reconfirming a previous confirmed Quote is.

### Display

Use compact vertical entries such as:

```text
10:42  Quote
Case 123  ABC電力
Price 99.85 -> 99.72

10:39  RFQ
Case 105  XYZ商事
Notl 50MM -> 100MM
```

Show only actual changed fields.

For Quote revisions, meaningful comparison fields include where available:

```text
Price
Yield
Simple
G-Spread
```

For RFQ revisions:

```text
Notl
Settle
Message
```

Client/Security never change within one Case and are identifiers, not diff fields.

### Data access

Do not implement this Drawer using an N+1 frontend pattern.

The current per-Case Revision/Quote history APIs are not sufficient for an efficient cross-Case diff feed, and current Quote history does not expose the numeric payload needed for the agreed diff.

Add a small **Sales-specific read model/query endpoint** if needed.

Suggested conceptual shape:

```text
GET /api/sales-rfqs/recent-revisions?limit=...
```

The exact naming may follow the current feature-oriented API convention.

The response should be typed enough that the frontend can render RFQ-vs-Quote revision entries without parsing arbitrary event JSON.

Do not change Domain semantics to build this feed.

Use a bounded result; around 50 recent items is sufficient for the first implementation.

No pagination UI is required in Phase 2.

---

## 31. Quote revisions and current quote changes

The persistent `Recent Revisions` feed is the operator-facing solution for:

> What formally changed while I was not looking?

Do not add a separate permanent unread/read state in Phase 2.

Do not add long-lived per-cell `changed since viewed` markers.

A short AG Grid change flash is optional but not required and must not be relied upon for business awareness.

---

## 32. Operation Log — explicitly not in Phase 2

Do not implement the previously considered Sales Operation Log.

For now:

- single operation success -> lightweight toast/normal UI state change;
- single failure -> visible error;
- bulk operation -> confirmation modal then per-item result display.

Keep command execution structured enough that a future local Operation Log can be added without rewriting every command path.

Do not create booking/report completion tracking.

That belongs to later Daily Review/post-trade discussion.

---

## 33. Work Pane command wiring

Avoid scattering command behavior independently across Work Pane, context menu, and shortcuts.

These are multiple **invocation surfaces**, not separate business implementations.

Prefer a Sales-local command/action layer that:

- checks current UI eligibility;
- invokes the existing mutation;
- handles success/error consistently;
- can be called by Work Pane, context menu, and shortcuts.

Do not create a generic application-wide command bus.

Do not copy RTK Query server state into another global Redux slice.

Feature-local hooks/reducer/context are acceptable where they reduce callback prop drilling.

---

## 34. Confirmation summary

### Work Pane — no confirmation

```text
Present
Unpresent
Reopen
Hit
Away
Cancel
Confirm Amendment
Discard Amendment
```

### Context menu — no confirmation

```text
Present
Unpresent
Reopen
Confirm Amendment
Discard Amendment
```

### Context menu — confirmation

```text
Hit
Away
Cancel
Outcome correction
```

### Keyboard shortcut — no confirmation

```text
Present
Unpresent
Reopen
single Amendment Confirm
```

### Keyboard shortcut — confirmation

```text
Hit
Away
Cancel
```

### Bulk

Always use the bulk confirmation modal.

Do not conflate these invocation paths.

---

## 35. Tests

Update/add focused frontend tests for:

- Work Pane mode derivation;
- selection stability across refresh;
- New/Draft validation and defaults;
- Amendment autosave/highlight/diff;
- no Amendment strip;
- context-menu eligibility and confirmation differences;
- shortcut behavior and typing safety;
- Bulk mode and bulk confirmation snapshot;
- no Bulk Hit;
- preset Filter predicates;
- safe/deferred SSE refresh;
- conflict preservation;
- Recent Revisions Drawer;
- explicit layout Save/Reset;
- filters not being persisted as layout.

Do not replace the suite with brittle screenshot/snapshot tests.

---

## 36. Backend/API tests for minimal read-model extensions

If Phase 2 extends the Sales backend projection, add focused tests for:

- `SalesId` returned correctly;
- current/closed confirmed Quote summary returned correctly;
- Manual quote missing values map cleanly;
- `StateSince` semantics;
- Recent Revisions excludes first-ever confirmation;
- RFQ confirmed-to-confirmed diff;
- Quote confirmed-to-confirmed diff;
- newest-first ordering;
- bounded result limit;
- current Sales authorization/visibility scope remains preserved unless deliberately changed in a separately reviewed decision.

Regenerate/update the OpenAPI artifact/schema through the repository's existing generation process when API contracts change.

Do not manually edit generated schema output.

---

## 37. Visual/interaction constraints

The finished Sales screen should feel like a dense front-office blotter, not a dashboard.

Prefer:

- information density;
- stable control locations;
- keyboard/mouse parity;
- low-latency direct operation;
- small restrained badges;
- subtle colors.

Avoid:

- large cards inside cards;
- excessive whitespace;
- giant status banners;
- bright full-row colors;
- repeated confirmation dialogs on the Work Pane;
- duplicated bulk bars;
- large explanatory text in normal operation;
- generic workflow wizard UI.

Keep the existing global dark mode.

---

## 38. Validation

Run at minimum:

```bash
dotnet test

cd src/Rfq.Web
npm test -- --run
npm run build
```

If `ag-grid-enterprise` is added, verify the application boots and the required Enterprise features are registered correctly.

Smoke-test `/sales` with:

- New RFQ;
- saved Draft;
- Waiting Quote;
- Quote Returned;
- Presented;
- pending Amendment;
- Cancelled;
- Hit;
- Away;
- multi-selection;
- remote update during normal viewing;
- remote update while editing;
- Recent Revisions Drawer.

Fix regressions introduced by the UX redesign.

---

## 39. Commit discipline

Keep this as a reviewable Sales UX phase.

A reasonable sequence is:

```text
1. Sales read-model/API additions required by UI
2. Sales Grid + Work Pane redesign
3. context menu / shortcuts / bulk modal / refresh behavior
4. Recent Revisions drawer + tests
```

The exact number of commits may vary, but do not mix unrelated Trader/Daily Review redesign.

Do not proceed into Daily Review or Booking workflow in this phase.

---

## 40. Final response

The implementation response should contain:

1. short summary of the final Sales screen structure;
2. backend/API read-model additions, if any;
3. confirmation/shortcut/context-menu behavior implemented;
4. auto-refresh behavior;
5. Recent Revisions implementation;
6. tests/build executed and results;
7. commit SHA(s);
8. any deviations from this instruction and why.

Do not continue into Trader or Daily Review redesign.
