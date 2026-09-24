# Codex Instruction — Rfq.Web 02a: State and Orchestration Correctness

## Position of this task

Baseline:

- repository: `sugaim/rfqfin`
- start from `main` at or after commit `ac6b2b6b78938659c1630c82608d28e188b1383e`
- that commit updates `docs/design.md` so Sales Recent Revisions is explicitly decoupled from normal Live catch-up

This task follows the completed 01 frontend cleanup work.

02a is intentionally **not** the Live scalability/cache task. Its purpose is:

> Make frontend state/orchestration match the canonical design, remove frontend reconstruction of business state, and make Draft/Amendment editing safe under optimistic concurrency.

This task may require small backend/API changes where they are necessary to implement those semantics correctly.

Before changing code, read:

- `docs/design.md`

Treat it as the single canonical design document.

Temporary files under `docs/refactoring/` are intentionally retained as refactoring history. Do not delete them.

---

# Core principles

Preserve these rules throughout the implementation:

1. Server query projections are authoritative business state.
2. Frontend interaction state is separate from server state.
3. The frontend must not predict lifecycle/ownership/quote/version transitions after mutations.
4. Live and Paused are display/reconciliation modes, not separate business-state models.
5. Optimistic concurrency remains authoritative; never silently rebase a failed edit onto a newer version.
6. Unsaved New input is FE-local.
7. Persisted Drafts are server-side working copies.
8. Short-lived editing/saving/calculation interactions may protect replacement; an open pane or Draft lifetime must not.
9. Dense grid-first UX and keyboard efficiency remain intentional.
10. Bulk operations retain per-Case partial-success semantics.

---

# 1. Frontend source organization

Complete the source ownership structure already documented in `docs/design.md`.

Target shape:

```text
src/
  app/
  pages/
    sales/
    trader/
    post-process/
  shared/
    ui/
    grid/
  services/
  generated/
  test/
```

Rules:

- Sales-only code belongs under `pages/sales`.
- Trader-only code belongs under `pages/trader`.
- Post Process-only code belongs under `pages/post-process`.
- `shared` is only for genuinely reused concepts.
- Existing generic grid-layout helpers are appropriate for `shared/grid`.
- Do not keep `features/sales`, `features/trader`, or `features/post-process` merely as aliases for pages.
- Do not introduce a generic `features/` bucket for page-owned code.
- Preserve `@/` imports and the no-relative-import convention inside `src`.
- Keep tests centralized under `src/test`.

This is a mechanical ownership migration; do not mix in broad unrelated rewrites.

---

# 2. Remove frontend reconstruction of business state

Current Sales code contains local business-state prediction such as:

- `applyRowAction`
- `reflectConfirmedAmendment`
- manual `currentVersion + 1`
- local lifecycle/status/quote mutation after successful commands

Remove this class of behavior.

Trader currently also patches persistent row state from mutation responses through mechanisms such as `onPatchRow` / `patchRow`. Rework persistent mutation reconciliation so the displayed RFQ state comes from the authoritative page query rather than local reconstruction.

Do not infer:

- lifecycle
- quote state
- ownership
- current/draft versions
- amendment confirmation result
- row membership

from the command that just succeeded.

Mutation responses may still be used internally for command-specific coordination where necessary, but they must not become a second frontend business-state source of truth.

---

# 3. Live / Paused state model

Use three distinct frontend state categories:

```text
authoritative query state
paused display snapshot
local interaction state
```

Do not maintain a second mutable Live copy of authoritative RFQ rows.

## 3.1 Live

In Live:

- the grid follows authoritative query state
- a successful mutation triggers authoritative page catch-up
- a relevant remote wake-up triggers authoritative catch-up when no protected interaction is active
- if a protected interaction is active, defer replacement and remember only that an update is pending
- when protection ends, perform one catch-up

Do not change the server-side SSE architecture in this task.

## 3.2 Paused

Entering Paused captures/preserves a display snapshot.

Remote changes while Paused:

- do not replace the Paused snapshot
- set a boolean `Updates pending`
- do not count events/Cases for the UI

Manual Refresh while Paused:

- re-read authoritative page state
- replace the Paused snapshot
- remain Paused
- clear pending indication on success
- surface failure explicitly
- no success toast is required

Paused -> Live:

- perform authoritative catch-up
- discard the Paused snapshot
- resume Live

Manual Refresh and Paused -> Live must be unavailable while a short-lived protected interaction is active.

## 3.3 Mutation reconciliation while Paused

There is intentionally no new single-Case page-projection endpoint in 02a.

After a successful Paused mutation:

1. execute the existing full page query once
2. use the fresh query only to reconcile the successful target Case(s)
3. ignore unrelated fresh rows

For each successful Case:

- if the fresh page query contains the Case, replace that Case in the Paused snapshot
- if the fresh page query no longer contains the Case, remove it from the Paused snapshot

For bulk operations:

- query once after the operation
- reconcile only `Succeeded` CaseIds
- leave `Failed`, `Skipped`, and unrelated rows unchanged

This preserves page-membership logic on the server without replacing the user's Paused view.

---

# 4. Protected interaction semantics

Protection must be short-lived and narrowly defined.

## Sales

Do **not** protect merely because:

- unsaved New exists
- a persisted Draft exists
- a work pane is open
- the Sales memo editor is open

Protect only the interaction window that would be unsafe to replace, for example:

- an actively edited persisted field
- its save/autosave request
- a confirmation/dialog interaction whose target/input would otherwise be invalidated

Sales inline row actions remain disabled in Live mode because row movement can change pointer targeting.

## Trader

CaseId/selection/pane-targeted operations may remain available in Live.

Protect only short-lived state such as:

- active cell editing
- in-flight calculation whose local interaction state would be invalidated
- quote confirmation interaction
- scratch-pricer operation where replacement would invalidate the active interaction

Do not turn Trader Live into a globally blocked mode.

---

# 5. Initial Draft editing and autosave

## 5.1 Unsaved New

Keep New FE-local until Save Draft or Confirm.

Unsaved New:

- does not have a CaseId
- does not block Live refresh of the grid
- can be discarded locally

## 5.2 Persisted Initial Draft

After Save Draft, the persisted Draft is a server-side working copy.

Field editing behavior:

- a field with no effective value change does nothing
- a changed field autosaves when editing completes
- do not require a separate long-lived Save button for ordinary persisted-Draft field updates
- protection lasts only for the active edit/save interval

## 5.3 Case-local autosave runner

Do not fire overlapping full-Draft requests against the same Case/version.

Implement a small Case-local autosave coordinator with these semantics:

```text
pending changes = field deltas
saving = at most one request per Case
```

Important:

- queue/merge field intent, not stale full payloads
- while one request is in flight, later field changes merge into pending deltas
- after a save succeeds, build the next full command from the latest acknowledged server result/state plus remaining pending deltas
- continue until no pending delta remains
- coalescing multiple quick edits is allowed and preferred
- do not create a generic application-wide queue framework

Bad:

```text
request 1 = full payload based on v3
request 2 = another full payload also based on v3
```

Good:

```text
pending.notional = ...
pending.settlementDate = ...
single Case runner serializes saves
```

Optimistic concurrency remains the final authority.

## 5.4 Autosave failure/conflict

Never silently rebase an edit onto a newer server version.

On version conflict:

- stop that Case's autosave chain
- surface the conflict explicitly
- obtain authoritative page state
- do not retry automatically with a newer version

On other save failure:

- surface the failure
- stop automatic retry
- do not fabricate successful local state

Active local input must not be silently overwritten during the protected edit/save interaction.

---

# 6. Amendment Draft

Match the canonical Amendment UX in `docs/design.md`.

## 6.1 Inline editing

For an inline editable amendment field:

- compare the attempted value with the effective value
- effective value = pending Draft value when present, otherwise current confirmed Revision value
- no effective change => no request
- real change => create or update the pending amendment Draft and persist the changed value

The frontend must not fabricate the new Draft/version locally.

## 6.2 Pane editing

Pane editing requires an explicit `Start Amendment` action.

If no pending Draft exists:

- `Start Amendment` creates a real server-side pending Draft initialized from the current confirmed Revision
- zero-difference at creation is valid

After the Draft exists:

- changed fields autosave on editing completion
- use the same Case-local serialization/delta principles as Initial Draft editing

If a dedicated backend/Application operation is needed for `Start Amendment`, add one rather than abusing a fake changed value.

## 6.3 Zero-difference invariant

A zero-difference pending Amendment Draft:

- is valid
- is not auto-discarded
- cannot be confirmed
- remains until it gets a real change or is explicitly discarded

Enforce the inability to Confirm on the server/domain boundary, not only with a disabled button.

The frontend should also disable/inhibit Confirm when the Draft has no effective change.

## 6.4 Authoritative Amendment result

Current `AmendmentResult` does not contain enough Draft terms for safe serialized autosave coordination.

Extend the smallest necessary Application/API result so the frontend can acknowledge the saved Draft without guessing.

Include the authoritative values needed for subsequent autosave composition, for example:

- Draft Revision ID
- Draft version
- Draft Notional
- Draft Settlement Date
- Draft Sales & Trading Message
- current Case version/status fields already required by the operation

Do not turn this into the broad API-contract cleanup planned for a later task.

The grid/display remains query-authoritative; this mutation result is for safe command sequencing, not local lifecycle reconstruction.

---

# 7. Memo editing

Memo is a separately versioned subresource.

Do not treat an open Sales memo editor as a global Live-refresh protection window.

For an active memo edit:

- keep the textarea/input value local while the user is editing
- capture the memo expected version associated with the value that editing began from
- allow the RFQ grid/query state to refresh independently
- Save uses the captured expected memo version
- version conflict is explicit
- do not silently rebase the local memo text onto a newer memo version
- Cancel discards local memo input

Trader inline memo editing may keep its short cell-edit/save protection.

---

# 8. Recent Revisions

Implement the design committed in `ac6b2b6b78938659c1630c82608d28e188b1383e`.

Recent Revisions must no longer be refetched by every Sales `catchUp()`.

Behavior:

- Drawer closed:
  - do not fetch/refetch Recent Revisions as part of ordinary Live/Paused RFQ reconciliation
  - relevant revision/quote changes may mark it stale
- Drawer opened:
  - fetch if not loaded or stale
- Drawer open:
  - relevant invalidation may trigger a coalesced refresh
- unrelated RFQ-list changes do not require a Recent Revisions refresh

Use the existing event/query information available today. Do not redesign SSE routing, introduce server subscriber routing, or build the 02b notification infrastructure here.

The existing Recent Revisions database query itself may remain structurally inefficient in 02a; removing it from every catch-up is the required change here.

---

# 9. Screen/component cleanup while touching this code

`SalesScreen.tsx` and `TraderScreen.tsx` are still long, but line count is not an acceptance criterion.

Do not split components merely to make the files smaller.

Extract only coherent UI concepts that now have a clear boundary.

Good candidates where they simplify the changed orchestration:

Sales:

- `RecentRevisionsDrawer`
- toolbar/status controls
- confirmation/dialog group where it becomes independently understandable

Trader:

- toolbar/status controls
- quote confirmation dialog

Do not force extraction of the main grid/work pane if it only creates large prop-plumbing with no clearer responsibility.

Prefer:

> Screen = page composition + genuinely page-level interaction wiring

over arbitrary function/file-size targets.

---

# 10. Tests

Update/add tests to lock the new semantics.

At minimum cover:

## Authoritative reconciliation

- frontend no longer increments versions or predicts lifecycle/status after mutation
- Live successful mutation is followed by authoritative page refresh
- Paused successful single mutation performs one page query and reconciles only the target
- Paused successful target absent from fresh query is removed
- Paused bulk reconciles only `Succeeded` CaseIds
- failed/skipped/unrelated Paused rows remain unchanged

## Live / Paused

- remote wake-up in Live catches up when unprotected
- protected remote wake-up defers replacement
- protection end performs one catch-up
- Paused remote wake-up sets boolean pending without replacing snapshot
- manual Paused Refresh replaces snapshot and remains Paused
- Paused -> Live catches up and drops snapshot
- Refresh / Paused -> Live are unavailable while protected
- unsaved New does not block Live

## Sales/Trader interaction safety

- Sales inline row commands are unavailable in Live
- Sales inline row commands remain available in Paused where otherwise eligible
- Trader CaseId-fixed operations remain usable in Live

## Initial Draft autosave

- unchanged field => no mutation
- changed field => autosave after edit completion
- two rapid edits on one Case do not issue overlapping stale-version saves
- pending field deltas survive while one save is in flight
- next request uses acknowledged version/state
- conflict stops automatic chaining/retry
- no silent rebase

## Amendment

- inline no-op edit does not create Draft
- inline real change creates Draft
- `Start Amendment` creates zero-difference Draft
- zero-difference Draft persists
- zero-difference Draft cannot Confirm at server/domain level
- subsequent changed fields autosave serially
- discard remains explicit

## Memo

- open memo editor does not globally block RFQ refresh
- remote row refresh does not overwrite active local memo text
- Save uses the memo version captured for the edit
- memo conflict is explicit

## Recent Revisions

- normal Sales catch-up does not refetch Recent Revisions
- closed Drawer does not query simply because RFQ Live state refreshed
- opening stale/unloaded Drawer fetches
- relevant invalidation while open refreshes
- unrelated list changes do not refresh it

Preserve existing tests that still encode intended behavior; rewrite tests that currently assert the old frontend-prediction model.

---

# 11. Explicit non-goals — reserved for 02b or later

Do **not** implement these in 02a:

## 02b — Live delivery / scalability

- server-wide Event/SSE dispatcher redesign
- removal of SSE-connection-per-client DB polling
- subscriber registry / audience routing by Sales user or Trader desk
- old/new audience routing
- Today RFQ in-memory read snapshot/cache
- immutable server snapshot / atomic swap
- server cache invalidation generations
- GET-from-memory optimization
- `DraftCreatedBusinessDate`
- current-business-day bounded Today read model
- current Sales/Trader query round-trip optimization
- historical Event scan optimization for `StateSince`
- Recent Revisions SQL/history optimization
- 100-concurrent-client load testing
- Redis
- PostgreSQL LISTEN/NOTIFY
- multi-API-instance cache coherence
- CaseId delta synchronization

## Later API cleanup

Do not perform broad cleanup of:

- handwritten `services/api.ts` contracts vs generated OpenAPI contracts
- generic generated-client architecture
- unrelated API naming/versioning work

Only make API contract changes directly required by 02a, especially `Start Amendment` and authoritative Draft save results.

## Other

Do not perform broad CSS cleanup.

Do not alter unrelated business rules, role authorization, bulk atomicity policy, pricing behavior, or Post Process semantics.

---

# 12. Implementation constraints

- Prefer existing Domain transitions/factories over mutable escape hatches.
- Add a specific transition/use case where a real business operation is missing.
- Keep Application responsible for authorization/orchestration and Domain responsible for state validity.
- Keep optimistic versions explicit.
- Do not use long-lived database locks.
- Do not silently swallow conflicts.
- Do not introduce a generic state-management framework merely for this refactor.
- Avoid abstractions whose only purpose is deduplicating a few lines.
- Preserve the explicit capability-contract style established in 01.
- Use `interface` for object-shaped frontend contracts and `type` for unions/aliases.
- Keep obvious local implementation values inferred.

---

# 13. Completion criteria

02a is complete when:

1. displayed RFQ business state is query-authoritative rather than locally predicted
2. Live/Paused behavior matches `docs/design.md`
3. Paused mutation reconciliation preserves unrelated snapshot rows
4. Initial Draft and Amendment edits autosave safely without overlapping stale full-payload writes
5. Amendment can be explicitly started and zero-difference Draft semantics are enforced
6. memo editing no longer creates long-lived global refresh protection
7. Recent Revisions is lazy/decoupled from normal Sales catch-up
8. page-owned frontend source lives under `pages/*` rather than page-named `features/*`
9. natural Screen subcomponents are extracted only where they improve responsibility clarity
10. tests cover the new correctness rules
11. all existing intended behavior outside this scope remains intact
12. 02b scalability/cache work has not been pulled into this change

At the end, report:

- changed files grouped by responsibility
- any backend/API changes required by 02a
- tests run and results
- any deliberately deferred 02b items discovered during implementation
