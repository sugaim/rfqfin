# Sales UX Follow-up Update

## Baseline

Repository:

```text
sugaim/rfqfin
```

Apply this update on top of:

```text
75bd562622a09e0c17326978e5cb2d65add88d91
```

This is a **follow-up to the existing Phase 2 Sales UX implementation**.

Do not reimplement Phase 2 from scratch.

The purpose of this update is to improve:

* row selection ergonomics;
* stable Work Pane behavior;
* fast per-row lifecycle actions;
* bulk operation UX;
* refresh stability during operator actions;
* shortcut discipline.

Do not redesign Trader or Daily Review in this change.

---

# 1. Selection — remove checkbox-oriented UX

Use an Excel-like row-selection model.

Required behavior:

```text
click           -> select one row
Ctrl/Cmd+click  -> add/remove row from selection
Shift+click     -> range selection
```

The selected rows are the bulk-operation target.

Do not depend on visible row-selection checkboxes as the primary UX.

Selection must be visually indicated by a narrow marker at the **left edge of the row**.

Do not use a selection background that obscures or changes the existing business-state row coloring.

Business-state color and selection indication have separate meanings:

```text
row background -> business/workflow state
left marker    -> selection
```

Keep stable row identity by `caseId`.

`Select All Filtered` may remain available as an explicit command/menu operation even though selection checkboxes are not shown.

---

# 2. Multiple selection must not automatically change the Work Pane

The current implementation derives:

```text
2+ selected rows -> Bulk mode
```

and automatically replaces the Work Pane with `BulkPane`.

Remove that behavior.

Selecting additional rows must **not cause the Work Pane to unexpectedly switch modes**.

The screen should remain spatially stable while the user selects rows.

For ordinary RFQ work:

* the Work Pane continues to represent the active/current Case;
* multi-selection exists independently as the current bulk target.

Do not make selection count itself determine the entire Work Pane mode.

---

# 3. Add an explicit Bulk tab to the Work Pane

Keep the existing right-side Work Pane.

Add a stable tab structure that includes a Bulk surface.

For example:

```text
[ RFQ ] [ Bulk ]
```

The exact visible label may be `Bulk` or `Bulk Ops`.

Do not automatically switch to the Bulk tab merely because multiple rows are selected.

The user explicitly opens the Bulk tab when bulk operation is intended.

The Bulk tab remains present even with zero or one selected row.

For zero/one selected rows, show a lightweight empty state such as:

```text
Select multiple RFQs for bulk operations
```

Do not dynamically add/remove the tab based on selection count.

---

# 4. Add fast row actions beside Case

Add a compact Action column near the left side of the Grid.

Preferred ordering:

```text
Case | Action | Client | Security | ...
```

The Action cell exposes only operations that are meaningful for the row's current state and current actor.

Examples include:

```text
Confirm
Discard
Present
Unpresent
Hit
Away
Cancel
Reopen
```

Do not show every possible button on every row.

The Action cell should remain compact.

Prefer abbreviated/small buttons where necessary.

The purpose is to let Sales process Cases from top to bottom without repeatedly moving into another UI surface.

Do not place the Action column at the far right.

---

# 5. Row Action confirmation behavior

Row actions are deliberate mouse operations on the target row.

For the agreed normal lifecycle operations, do **not** insert an additional confirmation dialog merely because the operation is strong.

In particular, row actions such as:

```text
Hit
Away
Cancel
```

may execute directly when eligible.

The Work Pane actions may likewise continue to execute directly.

Do not reintroduce confirmation dialogs on the Work Pane for these actions.

Bulk operations remain different and still use pre-execution confirmation.

Outcome correction and other exceptional/rare operations do not need to be promoted into the row Action column as part of this update.

---

# 6. Preserve existing bulk operations

Keep the existing supported Sales bulk operations:

```text
Present
Unpresent
Away
Cancel
Confirm Drafts
Discard Drafts
Confirm Amendments
Discard Amendments
```

Do not add Bulk Hit.

Do not remove Away/Cancel/Present/Unpresent merely because row actions now exist.

The two paths have different purposes:

```text
Row Action -> fast single-Case operation
Bulk       -> apply the same operation to many selected Cases
```

---

# 7. Bulk confirmation remains pre-execution

Bulk operations continue to require an explicit confirmation modal before execution.

The confirmation modal is the pre-execution review step.

Do not use the modal as the result display after execution.

Eligible/ineligible handling may continue to follow the existing bulk semantics.

The backend remains authoritative and must revalidate each item at execution time.

---

# 8. Move bulk results out of the modal

Current behavior keeps the bulk modal open and replaces its contents with per-item results.

Remove that extra post-operation step.

New flow:

```text
select rows
-> choose Bulk operation
-> confirmation modal
-> Apply
-> modal closes immediately
-> result appears in bottom Result Bar
```

Do not require the user to press another `Close` button after the server operation finishes.

---

# 9. Bottom Result Bar

Add a compact result area at the bottom of the Sales workspace.

Collapsed example:

```text
Bulk Away: 18 ok / 2 skipped / 1 failed    [Details]
```

The bar represents the most recent bulk result.

Normal collapsed height should be small, approximately one status-bar row.

`Details` expands upward and shows per-item details, primarily for non-success items.

Useful columns:

```text
Case | Result | Code | Message
```

If an operation returns a diagnostic/log identifier in the future, the result area should be able to display it.

Behavior:

* all success -> low-emphasis success state;
* skipped items -> warning indication;
* failed items -> error indication;
* expanded details may omit successful rows if that keeps the display compact;
* closing/collapsing details does not discard the most recent result;
* the next bulk execution replaces the previous result.

This Result Bar should be reusable later by Trader, but do not create an unnecessary generic framework in this Sales update.

---

# 10. Live / Pause refresh mode

Introduce an explicit refresh mode:

```text
Live
Paused
```

Default normal operation is `Live`.

## Live

While Live:

* existing remote-update/SSE behavior continues;
* normal query refresh may update the Grid;
* Sales row Action buttons described above are disabled.

## Paused

When switched to Paused:

* stop automatic application of remote refreshes to the visible RFQ list;
* keep the current visible snapshot stable;
* continue receiving remote-change notifications;
* track that updates are pending.

Display a compact indication such as:

```text
Paused · 4 updates pending
```

During Paused mode:

* Sales row Action buttons become enabled;
* the user may process Cases without rows moving underneath the pointer.

The purpose of Pause is not authorization.

It is an operator-stability mechanism.

Backend optimistic concurrency remains authoritative.

If another actor changed a Case while the screen was paused, the action may fail with the normal conflict semantics.

---

# 11. Returning from Paused to Live

When the user resumes Live mode:

1. refetch the latest snapshot once;
2. allow current sort/filter rules to be reevaluated;
3. clear the pending-update count;
4. resume normal live refresh behavior.

Do not attempt to replay every remote event into the visible Grid.

The UI remains latest-snapshot based.

---

# 12. Manual Refresh

Keep an explicit refresh control.

Use a compact refresh icon rather than a large text button where practical.

The refresh control belongs at the far/right utility side of the toolbar.

Manual Refresh works in either mode:

```text
Live   -> fetch latest now
Paused -> fetch latest now but remain Paused
```

A manual refresh is an explicit user request to replace the currently visible snapshot.

---

# 13. Action-result behavior while Paused

After a successful row Action while Paused:

* update the affected row sufficiently for the user to see the new state;
* do not force a full refresh solely because the action succeeded;
* preserve the operator's working position where practical.

A later:

```text
Refresh
```

or:

```text
Paused -> Live
```

performs the full latest-snapshot reconciliation.

If the action fails because of version/state conflict:

* show the error clearly;
* do not silently reinterpret the requested operation;
* allow the user to refresh/reconcile.

---

# 14. Shortcut cleanup

Reduce Sales shortcuts substantially.

Keep:

```text
Esc
Alt+L
Alt+N
Alt+Enter
```

Semantics:

```text
Esc       -> cancel/close the current transient edit/dialog as appropriate
Alt+L     -> toggle Live / Paused
Alt+N     -> New RFQ
Alt+Enter -> existing Draft / Amendment confirmation workflow
```

Remove Sales lifecycle shortcuts such as:

```text
Alt+P -> Present
Alt+U -> Unpresent
Alt+R -> Reopen
Alt+H -> Hit
Alt+A -> Away
Alt+C -> Cancel
```

These operations now have strong mouse-based paths through:

* row Action;
* Work Pane;
* Bulk tab where applicable.

Do not add shortcuts merely because an operation exists.

Shortcuts should be reserved for operations where keyboard access materially reduces friction.

As before, global shortcuts must not fire while the user is typing into an input/editor unless explicitly intended.

---

# 15. Confirmation behavior after shortcut cleanup

The existing distinction between invocation surfaces should be simplified by the new UI.

For this update:

```text
Row Action       -> no extra confirmation for normal lifecycle actions
Work Pane        -> no extra confirmation for normal lifecycle actions
Bulk             -> confirmation modal
```

The removed Hit/Away/Cancel shortcuts therefore no longer require their old shortcut confirmation behavior.

Do not add new lifecycle shortcuts to compensate.

Context-menu behavior does not need a broad redesign in this change unless required to keep behavior coherent.

---

# 16. Grid layout persistence

Preserve explicit layout persistence.

Do not autosave Grid layout.

`Save Layout` and `Reset Layout` may be moved into the Grid context menu if that keeps the top toolbar cleaner.

If the loaded layout payload is invalid or incompatible:

* fall back to the built-in default layout;
* do not break the screen;
* do not automatically overwrite the stored server value;
* a later explicit Save writes a valid replacement.

Do not persist transient selection, Live/Pause state, or pending-update count as part of Grid layout.

---

# 17. Preserve existing Phase 2 behavior

Do not regress:

* New RFQ workflow;
* Draft workflow;
* Amendment autosave/upsert behavior;
* Amendment diff/highlighting;
* Sales Memo;
* Contact Owner handoff;
* Recent Revisions;
* current Sales preset filters;
* quote summary;
* optimistic concurrency;
* SSE/deferred-refresh protection while actively editing;
* explicit Grid layout persistence;
* dark dense blotter styling.

This change is an interaction refinement, not a Domain redesign.

---

# 18. Tests

Update frontend tests to cover at least:

* no checkbox-dependent selection UX;
* Ctrl/Cmd multi-select behavior where testable;
* Shift/range selection configuration;
* selection marker styling/class;
* 2+ selected rows do not automatically replace the RFQ Work Pane;
* Bulk tab exists independently of selection count;
* Bulk tab empty state with insufficient selection;
* row Action eligibility;
* row Hit/Away/Cancel execute without confirmation;
* existing eight Bulk operations remain;
* Bulk Hit remains unavailable;
* bulk confirmation modal closes after execution;
* result appears in Result Bar;
* Result Bar details for skipped/failed items;
* Live/Pause toggle;
* row Actions disabled in Live and enabled in Paused;
* pending remote-update count while Paused;
* resume performs one latest-snapshot refresh;
* manual Refresh works while Paused without switching back to Live;
* shortcut set is reduced to the agreed keys;
* removed lifecycle shortcuts no longer fire.

Run:

```bash
dotnet test

cd src/Rfq.Web
npm test -- --run
npm run build
```

---

# 19. Scope discipline

Do not implement Trader redesign in this change.

Do not implement Daily Review redesign.

Do not add Bulk Hit.

Do not redesign Domain lifecycle semantics.

Do not create a generic command bus or generic multi-screen UX framework.

Keep this as a reviewable **Sales UX follow-up** on top of the current Phase 2 implementation.

---

# 20. Final response

Report:

1. Sales selection changes;
2. Work Pane/Bulk-tab changes;
3. row Action implementation;
4. Live/Pause behavior;
5. Result Bar behavior;
6. shortcut removals/additions;
7. tests/build results;
8. commit SHA(s);
9. any deviation from this instruction.
