# Codex Instruction — Rfq.Web Structure and Test Cleanup

## Goal

Refactor `src/Rfq.Web` for human readability and maintainability **without changing observable application behavior**.

This task covers:

1. Frontend test organization cleanup.
2. Splitting oversized Sales/Trader screen files by responsibility.
3. Moving pure logic and reusable UI fragments into appropriate files.
4. Removing obvious dead frontend API surface that is proven unused.
5. Improving local readability through sensible file boundaries, blank lines, and minimal comments where intent is non-obvious.

This is a **behavior-preserving structural refactor**. Do not intentionally change business rules, API contracts, refresh semantics, authorization semantics, or UX behavior.

---

## Read this first

Before changing code, read:

- `docs/design.md`

Treat `docs/design.md` as the **single canonical design document**.

Do not rely on old deleted/refactoring documents or repository history as active specification unless needed only to understand why current code exists.

Pay particular attention to these design constraints:

- Frontend does not own business authority.
- Sales, Trader, and Post Process are distinct operational surfaces.
- Dense desktop/grid-first UX is intentional.
- Keyboard efficiency is intentional.
- Live / Paused behavior is intentional.
- Remote updates must not silently overwrite actively edited operator state.
- SSE is only a wake-up signal, not authoritative application state.
- Bulk operations are operation-specific and allow per-case partial success.
- Grid layout persistence is frontend-owned and intentionally stored as opaque JSON.
- Feature-first source organization is preferred; technical buckets should be used only for genuinely cross-cutting concerns.

If a cleanup would conflict with these rules, preserve the current behavior and structure the code around the design rather than simplifying away the behavior.

---

# Scope

Work only in `src/Rfq.Web` unless a minimal test/build configuration change outside that directory is required.

Do not change backend behavior.

Do not change API endpoint behavior.

Do not change generated API schema contents.

Do not perform the later API-contract cleanup in this task. `src/generated/api-schema.ts` and the handwritten API contract duplication will be handled separately.

---

# Part 1 — Move frontend tests under `src/test`

The implementation tree should no longer contain `*.test.ts` / `*.test.tsx` files next to production code.

Move frontend tests under `src/Rfq.Web/src/test`.

Use a structure similar to:

```text
src/test/
  app/
  sales/
  trader/
  post-process/
  grid/
  services/
  support/
```

Exact file names are up to you, but keep the structure easy to navigate.

## Specific expectations

### Split the current large `App.test.tsx`

The current root `src/App.test.tsx` is too broad and mixes tests for multiple features.

Split it into focused test files, for example:

- AppShell tests
- SalesScreen tests
- TraderScreen tests

Do not preserve the large file just because it already works.

### Extract shared test support

Move shared testing infrastructure such as:

- AG Grid mocks/test doubles
- common fixtures
- repeated factories/builders
- reusable render helpers

into `src/test/support`.

Do not over-generalize test helpers. Extract only support that is genuinely shared.

### Preserve test meaning

Do not rewrite tests merely to make them shorter.

The test suite currently protects important behavior, including:

- Excel-like grid selection behavior
- Live / Paused semantics
- protected editing state
- bulk operation availability and partial-success presentation
- Trader calculation locking
- stale calculation response suppression
- calculation timeout recovery
- scratch pricer provenance
- Post Process staged edits and navigation protection

Preserve these behavioral checks.

If test locations require Vitest or TypeScript configuration changes, make the minimum required change.

---

# Part 2 — Refactor `SalesScreen.tsx`

`SalesScreen.tsx` is currently very large and contains multiple distinct responsibilities.

Refactor it into feature-local files under `features/sales`.

Do **not** split files solely to reach an arbitrary line-count target.

Split where doing so improves one or more of:

- discoverability
- cohesion
- readability
- testability
- accidental coupling
- local reasoning

Likely candidates include:

- Sales grid definition / grid rendering
- work pane
- lifecycle pane
- quote summary
- bulk action UI
- bulk result UI
- form-related UI
- context-menu construction
- keyboard interaction
- feature-local view helpers

Use `.ts` for pure/non-React logic where appropriate.

Use custom hooks only when there is meaningful stateful React behavior to isolate. Do not create hooks merely as wrappers around a few local variables.

## Important behavior to preserve

Do not change:

- New RFQ flow
- Draft flow
- Amendment flow
- Work Pane behavior
- selection behavior
- current active row behavior
- keyboard shortcuts
- context-menu behavior
- Live / Paused restrictions
- protected-state reporting
- grid layout Save / Load / Reset behavior
- Recent Revisions behavior
- bulk operations
- partial-success result display
- current confirmation behavior

Do not “simplify” the UI by removing intermediate state that exists to protect operator input.

## Important restriction

The current Sales paused-mode local state transition logic is known to deserve separate review.

Do **not** redesign or fix that logic in this task.

In particular, do not change the semantics of:

- `applyRowAction`
- `reflectConfirmedAmendment`
- local paused snapshot patching

You may move code, rename code, or isolate code for readability, but preserve behavior exactly.

A later task will replace frontend-predicted business state with authoritative mutation-response patching.

---

# Part 3 — Refactor `TraderScreen.tsx`

`TraderScreen.tsx` is also oversized and currently combines several distinct systems.

Refactor it into feature-local files under `features/trader`.

Likely responsibility boundaries include:

- active RFQ grid
- column definitions
- search panel / search grid
- operations pane
- pricer pane
- confirmation UI
- result bar
- keyboard interaction
- grid layout helpers
- calculation interaction state

Again, split by responsibility, not by arbitrary size.

## Trader calculation behavior is high-risk

The following behavior must remain intact:

- only the relevant Case is locked while calculating
- unrelated RFQs remain operable
- stale calculation responses do not overwrite newer intent
- refresh generation invalidates obsolete calculation results
- timeout releases the Case
- failure state is surfaced compactly
- successful results reconcile correctly
- Paused mode local patch behavior remains unchanged in this task

The current coordination involving concepts such as:

- request sequence
- per-Case request tracking
- in-flight Case tracking
- refresh generation
- calculation state

is intentional concurrency control.

You may isolate this logic into a feature-local hook/controller if that clearly improves readability, but preserve semantics exactly.

Add a short explanatory comment if necessary to explain **why stale-result suppression exists**. Do not add comments that merely restate the code.

## Preserve existing Trader behavior

Do not change:

- Pick / Release / Assign / Take Over behavior
- quote editing
- calculated/manual mode switching
- Confirm flow
- Hit/Away/Cancel/Reopen/Correction behavior
- memo behavior
- search behavior
- scratch pricer behavior
- pricer provenance validation
- keyboard shortcuts
- Live / Paused behavior
- grid persistence behavior
- bulk behavior
- partial-success display

---

# Part 4 — Small structural cleanup

While performing the refactor, clean up small structural issues only when they are unambiguous.

Examples:

- remove `SalesScreenProps.onBulkClose` if it remains proven unused
- move feature-local types to more appropriate feature-local modules
- make naming consistent where current placement is clearly accidental
- remove dead imports
- remove dead local helpers
- add blank lines between meaningful logical groups
- add minimal comments around non-obvious intent/caveats

Do not use “unused => private/delete” mechanically.

When deciding whether something should remain exported/public, determine whether it belongs to the feature/module API or is only an implementation detail.

Prefer fewer exports when the symbol is only used internally.

---

# Comments and formatting guidance

The goal is readability for a human maintainer.

Do not add doc comments everywhere.

Add comments only when one of these applies:

- public feature API has non-obvious semantics
- behavior exists for a subtle reason
- concurrency or stale-state protection is easy to break
- an implementation intentionally differs from an obvious simpler alternative
- a caveat is important for future modification

Use blank lines to separate logical blocks.

Avoid walls of uninterrupted statements where distinct phases exist.

Do not add comments such as:

```ts
// Set loading to true
setLoading(true)
```

Prefer comments explaining intent, for example:

```ts
// Ignore responses from a calculation started before the latest refresh.
```

---

# Architecture constraints

Preserve the current broad direction:

```text
Workspace
  -> feature screen / feature-local UI
  -> feature-local model / helpers
  -> API hooks
```

Do not introduce a generic “screen framework”.

Do not create a large shared frontend abstraction merely because Sales and Trader look superficially similar.

Do not extract business rules into generic utilities unless they are truly common and already equivalent.

Feature-local duplication is preferable to an abstraction that obscures business meaning.

---

# Files that should generally remain untouched

Do not manually edit:

- `src/generated/api-schema.ts`

Do not perform the planned API decomposition in this task.

Do not perform CSS architecture cleanup beyond what is minimally necessary to support component/file extraction.

A later task will handle:

- `services/api.ts` decomposition
- generated API type adoption
- CSS organization
- shared Live/Paused refresh abstraction
- authoritative Sales paused-state reconciliation

---

# Known behavior/spec questions — do not fix here

There are several areas that may represent product/spec mismatches.

Do not change them in this structural refactor:

1. Save Draft validation appears stricter than the design wording around minimum Client + Security.
2. Sales bulk draft operations may not currently filter by Contact Owner at the UI level.
3. Sales inline amendment editability may not currently filter by Contact Owner at the UI level.

These require separate behavior/spec review.

Preserve current behavior for now.

---

# Verification

After the refactor, run the relevant frontend checks.

At minimum:

```bash
npm test
npm run build
npm run lint
```

Also run the repository's frontend formatting/quality command if available.

Fix issues introduced by the refactor.

Do not “fix” unrelated application behavior merely because a test becomes inconvenient.

If a test fails because the refactor changed behavior, restore the original behavior.

---

# Completion criteria

This task is complete when:

- all frontend tests are under `src/test`
- the large `App.test.tsx` has been split by concern
- shared test mocks/helpers are under `src/test/support`
- `SalesScreen.tsx` is decomposed into coherent feature-local units
- `TraderScreen.tsx` is decomposed into coherent feature-local units
- pure logic is moved out of React components where that materially improves readability
- high-risk state/concurrency semantics remain unchanged
- no intended feature has been removed
- no API contract has been intentionally changed
- no UX behavior has been intentionally changed
- generated API code remains untouched
- tests/build/lint pass
- the resulting source tree is easier for a human maintainer to navigate

---

# Deliverable

Make the changes directly in the repository.

At the end, provide a concise summary containing:

1. files/modules introduced or moved
2. major responsibility boundaries created
3. dead code removed
4. any areas deliberately left unchanged because they belong to later cleanup
5. verification commands run and their results

Do not include unrelated redesign proposals in the implementation commit.
