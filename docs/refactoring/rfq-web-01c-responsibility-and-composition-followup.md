# Codex Instruction — Rfq.Web 01 Follow-up: Responsibility and Composition Cleanup

## Position of this task

This task is the **follow-up to both frontend cleanup commits**:

- 01a: `8b7964e0b1492fc1a478de70b9b41ef45358d20a`
- 01b: `ed134e585840c41a63d53a8ea5d84435ec9a1fc3`

The purpose is to complete the original **01 goal**:

> Improve `src/Rfq.Web` for human readability and maintainability while preserving observable behavior.

01a and 01b made substantial progress. Do not repeat them mechanically.

First inspect the current code after 01b, then make only justified follow-up changes.

This remains a **behavior-preserving structural refactor**.

Do not change:

- business rules
- authorization semantics
- API contracts
- Live / Paused semantics
- optimistic concurrency semantics
- bulk partial-success semantics
- keyboard behavior
- intended UX behavior

---

# Read this first

Before changing code, read:

- `docs/design.md`

Treat it as the single canonical application design document.

Preserve in particular:

- frontend is not the business authority
- Sales / Trader / Post Process are distinct operational surfaces
- dense desktop/grid-first UX is intentional
- keyboard efficiency is intentional
- Live / Paused behavior is intentional
- actively edited operator state must not be silently overwritten
- SSE is a wake-up signal, not authoritative application state
- bulk operations allow per-case partial success
- grid-layout persistence is frontend-owned
- feature-first source organization is preferred

Temporary files under `docs/refactoring/` are intentionally being kept as cleanup history during this multi-step refactoring effort.

Do not remove them in this task.

---

# Current state after 01a / 01b

01b successfully introduced several useful boundaries.

Examples that should generally be preserved:

- Sales and Trader column definitions were extracted
- Trader search UI was extracted
- Trader working-quote calculation coordination was extracted to `useWorkingQuoteCalculation`
- a shared bulk-result presentation component was introduced
- Sales RFQ editor UI was extracted
- exported component boundaries now use named Props types more consistently
- child components no longer borrow parent `ScreenProps[...]` types merely to reuse callback signatures
- conservative ESLint Stylistic blank-line enforcement was added

Do not undo these improvements unless the current implementation clearly demonstrates a better responsibility boundary.

However, meaningful residual complexity remains.

At commit `ed134e5...`, notable files are approximately:

```text
TraderScreen.tsx               ~947 lines
TraderWorkspace.tsx            ~422 lines
TraderOperationSections.tsx    ~467 lines

SalesScreen.tsx               ~1051 lines
SalesWorkspace.tsx             ~484 lines
SalesWorkPane.tsx              ~360 lines
```

Line count alone is not the problem.

The concern is that multiple independent concepts remain concentrated in these files.

---

# Core design principle for this follow-up

Do not optimize for "small files".

Optimize for:

- one coherent responsibility per component/module
- understandable dependency direction
- local reasoning
- explicit feature contracts
- predictable code location
- minimal unrelated state in the same component
- orchestration close to the concept it coordinates

A file may remain large if it represents one coherent concept.

A smaller file is not automatically better.

---

# Type style for this task

Use this repository-level convention consistently:

> **Explicit at boundaries; inference for obvious local implementation details.**

Prefer `interface` for object-shaped contracts such as:

- component Props
- action/capability contracts
- controller inputs/outputs
- feature-level service contracts

Prefer `type` for:

- literal unions
- discriminated unions
- aliases
- tuples
- intersections/compositions where that is the natural expression

Examples:

```ts
interface TraderOwnershipActions {
  pickUp: ...
  release: ...
}

type TraderPaneTab = 'operations' | 'pricer'
type RfqOutcome = 'Hit' | 'Away'
```

Do not add noise such as:

```ts
const count: number = rows.length
const quoted: boolean = ...
```

when TypeScript inference is obvious.

Exported components/functions/hooks should have clear named input/output types where practical.

---

# 1. TraderScreen — complete the responsibility split

`TraderScreen` is the main residual Trader hotspot.

It currently coordinates several independent concepts:

- active-grid selection and interaction
- search state and execution
- confirmation state
- operations-pane state
- scratch-pricer state and provenance
- grid-layout state for three grids
- keyboard shortcuts
- result/error state
- working-quote calculation coordination
- top-level rendering/layout

`useWorkingQuoteCalculation` is already a good extraction.

Preserve it unless a minor boundary adjustment is required.

The Screen should move toward:

> compose feature-local interaction controllers + render major UI regions

rather than implementing every interaction itself.

---

## 1.1 Extract Trader search interaction state

`TraderSearchSection` owns much of the rendering, but `TraderScreen` still owns:

- `searchOpen`
- `searchFiltersOpen`
- `searchPreset`
- `searchFilters`
- `searchResult`
- `searching`
- `executeSearch`
- search-grid lifecycle/layout integration

These belong to one coherent search interaction concept.

Consider a feature-local controller/hook such as:

```ts
useTraderSearch(...)
```

or an equivalent explicit module.

Its contract should be named and typed.

Do not change search behavior.

Do not move business authority into the hook.

The top-level Screen should not need to know every internal search-state setter if a smaller intent-level contract is sufficient.

---

## 1.2 Extract Trader scratch-pricer interaction state

`TraderPricerPane` is mostly presentational, while `TraderScreen` still owns:

- scratch input state
- scratch result
- provenance
- busy state
- source-row resolution
- `setScratchIdentity`
- load-from-selected logic
- scratch calculation
- Apply eligibility
- Apply behavior
- clear/reset behavior

These form one coherent **scratch-pricer interaction model**.

Extract them into a feature-local controller/hook, for example:

```ts
useTraderScratchPricer(...)
```

or equivalent.

Preserve:

- provenance behavior
- source-term validation
- stale-source protection
- Apply eligibility
- Manual-mode restriction
- calculation behavior

Do not create a generic pricing hook shared with unrelated screens.

---

## 1.3 Extract Trader-specific multi-grid layout coordination

`TraderScreen` currently coordinates three grid APIs:

- main
- search
- confirm

and owns:

- grid refs
- default column-group states
- persisted config lookup
- layout-menu construction
- applying persisted layouts
- grid initialization

This is one coherent Trader-specific responsibility.

Consider extracting a small controller/hook such as:

```ts
useTraderGridLayouts(...)
```

or equivalent.

Reuse the existing generic functions in:

```text
features/grid/gridLayout.ts
```

Do not create a generic screen framework.

The new layer should only coordinate Trader's three grids.

---

## 1.4 Review confirmation flow as one concept

The confirmation workflow includes:

- confirmable row derivation
- `confirmRows`
- opening/cancelling confirmation
- single confirmation
- bulk confirmation
- result creation
- confirmation-grid integration

If this still contributes meaningfully to Screen complexity after the other extractions, isolate it as a coherent controller/component.

Do not extract it merely to reduce line count.

---

## 1.5 Review Trader keyboard behavior

Keyboard shortcuts are intentional product behavior.

If the keyboard effect still has a broad dependency surface after other responsibilities are extracted, consider a feature-local hook such as:

```ts
useTraderKeyboardShortcuts(...)
```

Only do this if it improves readability.

Do not create a generic application-wide shortcut framework.

---

# 2. Trader operations — separate the real concepts

`TraderOperationSections.tsx` still combines several different feature concepts:

- Ownership actions
- Quote actions
- Contact Owner / lifecycle actions
- Bulk actions
- action contracts
- generic runner contracts

This is a concrete candidate for further responsibility-based separation.

A reasonable direction is:

```text
features/trader/operations/
  TraderOperationsPane.tsx
  OwnershipActions.tsx
  QuoteActions.tsx
  ContactOwnerActions.tsx
  BulkActions.tsx
  operationTypes.ts
```

Exact file names are not mandatory.

The important point is that:

- Ownership
- Quote
- Lifecycle / Contact Owner
- Bulk

are different concepts and should not remain in one file merely because they render in one side pane.

Do not create a file for every trivial five-line component.

Split only meaningful concepts.

---

# 3. Trader operation UI should express intent, not reconciliation mechanics

A remaining concern is the generic operation runner:

```ts
export type TraderOperationRunner = <T>(
  action: () => Promise<T | void>,
  patch?: (row: TraderRfq, value: T) => TraderRfq,
  row?: TraderRfq,
) => Promise<T | void>
```

Operation UI components receive this runner and choose patch functions such as:

- `patchOwnership`
- `patchWorkingQuote`
- `patchLifecycle`
- `patchPresentation`
- `patchContactOwner`

This exposes mutation/reconciliation mechanics to presentation-oriented components.

Review and improve this boundary.

Prefer, where practical:

- controller/orchestration code chooses mutation + reconciliation semantics
- UI sections receive intent-level callbacks

For example:

```ts
interface OwnershipActionsProps {
  onPickUp: () => void
  onRelease: () => void
  onAssign: (targetTraderId: string) => void
  onTakeOver: () => void
}
```

rather than a generic runner plus patch function selection inside the UI.

Do not force this exact API if another explicit design is cleaner.

The intended separation is:

> UI expresses the user's requested action.  
> Controller code owns execution, error handling, refresh/reconciliation, and paused-mode patch mechanics.

Preserve behavior exactly.

---

# 4. TraderWorkspace — keep the composition root, reduce inline adapter noise

Passing callbacks from a Workspace/container into a Screen is normal React architecture.

The problem is **not** that `TraderWorkspace` is the top-level composition point.

The issue is that it currently contains a large number of API adapters directly inside JSX:

```tsx
<TraderScreen
  onPickUp={(row, confirmed) => ...}
  onRelease={(row) => ...}
  onAssign={(row, targetTraderId) => ...}
  onTakeOver={(row) => ...}
  onCalculate={(row, driver, value, slide) => ...}
  ...
/>
```

This keeps dependencies explicit, which is good, but at this scale the render expression itself becomes the API-adapter implementation.

Refactor this while preserving the Workspace as the composition root.

---

## 4.1 Bind API operations into named capability contracts before JSX

Group API adapters only by real responsibility.

Candidate groups:

```text
ownership
workingQuote
lifecycle
contactOwner
memo
bulk
search
pricer
gridLayout
refresh
```

For example:

```ts
interface TraderOwnershipActions {
  pickUp: ...
  release: ...
  assign: ...
  takeOver: ...
}

interface TraderLifecycleActions {
  present: ...
  unpresent: ...
  withdraw: ...
  close: ...
  cancel: ...
  reopen: ...
  correctOutcome: ...
}
```

Construct named, typed capability objects before rendering.

The final JSX should read more like composition than implementation.

---

## 4.2 Extract adapter hooks only when a group is substantial

If a capability requires enough RTK Query mutations and request mapping to justify its own module, a feature-local adapter hook is reasonable:

```ts
useTraderOwnershipActions(...)
useTraderWorkingQuoteActions(...)
useTraderLifecycleActions(...)
```

Do not create one hook per endpoint.

Do not create dozens of tiny wrappers solely to hide lines.

The goal is to expose meaningful feature capabilities.

---

## 4.3 Keep API request construction out of visual components

Request mapping such as:

```ts
{
  caseId: row.caseId,
  expectedVersion: row.currentVersion,
  ...
}
```

belongs in the Workspace/adapter layer, not in presentation components.

Preserve that direction.

---

# 5. TraderScreen Props — group cohesive capabilities

`TraderScreenProps` currently exposes many operation callbacks at one level.

This is explicit, but increasingly difficult to navigate.

Where concepts are cohesive, group them into named capability contracts.

A possible direction:

```ts
interface TraderScreenProps {
  rfqs: TraderRfq[]
  traders: UserOption[]
  users: UserOption[]
  currentUserId: string

  ownership: TraderOwnershipActions
  workingQuote: TraderWorkingQuoteActions
  lifecycle: TraderLifecycleActions
  contactOwner: TraderContactOwnerActions
  memo: TraderMemoActions
  bulk: TraderBulkActions

  ...
}
```

Do not group unrelated view-state inputs merely to reduce prop count.

Do not create one opaque mega-object such as:

```ts
actions: { ...everything... }
```

Grouping must communicate real responsibility.

---

# 6. SalesScreen — complete the responsibility split

`SalesScreen` remains the main Sales hotspot.

Useful extractions already exist:

- `SalesRfqEditor`
- `salesColumns`
- `SalesBulkUi`
- `SalesWorkPane`

However, the Screen still owns several independent interaction models:

- New / Draft editing workflow
- selection
- lifecycle execution
- paused row-action reconciliation trigger
- amendment editing
- bulk workflow
- memo state
- Contact Owner state
- context-menu construction
- keyboard behavior
- Recent Revisions drawer state
- grid state/layout
- confirmation dialog state
- result/error state
- top-level rendering

Review and separate the meaningful concepts below.

---

## 6.1 Extract Sales New / Draft editor controller

The Screen currently owns:

- `newIntent`
- form lifecycle
- row-to-form population
- defaults resolution
- request DTO construction
- validation
- create/update/confirm handling
- draft discard handling
- loading/error coordination for defaults

These form a coherent **Sales RFQ editing workflow**.

Consider extracting a controller/hook such as:

```ts
useSalesRfqEditor(...)
```

or equivalent.

The existing `SalesRfqEditor` component should remain focused on rendering/input.

The controller should expose intent-level commands rather than leaking Form internals unnecessarily.

### Important

Do not change the known Save Draft behavior/spec question in this task.

Even if the current validation appears stricter than the design wording, preserve current observable behavior.

That is a separate product/spec task.

---

## 6.2 Extract Sales bulk workflow

The Screen currently owns:

- selected-row bulk snapshot construction
- eligibility snapshot
- confirmation dialog state
- execution
- skipped-result synthesis
- result state/expansion
- reload after completion

`BulkPane` and result presentation are already extracted, but the **bulk controller** remains in `SalesScreen`.

This is a coherent candidate for extraction, for example:

```ts
useSalesBulkOperations(...)
```

or equivalent.

Preserve:

- snapshot semantics
- per-case eligibility
- partial-success result handling
- confirmation behavior
- current reload behavior

Do not change bulk authorization rules in this task.

---

## 6.3 Extract Sales lifecycle / operation execution

`SalesScreen` still owns:

- generic `run`
- `executeCommand`
- `executeRowCommand`
- confirmation routing
- paused-mode `onRowActionApplied`
- error/conflict handling

These represent an operation execution/reconciliation concept.

Review whether they should become a feature-local controller.

The controller should own:

- execution
- conflict/error state
- live-mode reload
- paused-mode follow-up notification

while UI components express intent.

### Important

Do not replace the existing frontend-predicted paused-state behavior yet.

The later state/orchestration task will address authoritative mutation-response reconciliation.

For this task, preserve current semantics exactly.

---

## 6.4 Extract Sales context-menu construction

The Sales context menu currently combines:

- lifecycle commands
- confirmation policy
- Create New from Existing
- copy helpers
- selection commands
- grid-layout menu

This is a coherent grid interaction concept.

Move it into a feature-local builder/hook if doing so reduces `SalesScreen` complexity.

For example:

```ts
buildSalesContextMenu(...)
```

or:

```ts
useSalesContextMenu(...)
```

Choose based on whether React hooks are truly needed.

Do not change available menu commands or confirmation behavior.

---

## 6.5 Review Sales keyboard behavior

The Sales keyboard handler coordinates:

- Escape behavior
- Live / Paused toggle
- New RFQ
- Confirm
- amendment confirmation
- text-input protection

If it remains substantial after other extractions, consider a feature-local shortcut hook.

Do not create a global shortcut framework.

Preserve keyboard behavior exactly.

---

# 7. SalesWorkPane — split actual concepts

`SalesWorkPane.tsx` currently contains multiple concepts:

- Work Pane header
- confirmed quote summary
- RFQ facts
- pending amendment summary/actions
- lifecycle actions
- Contact Owner handoff
- memo editor

This is more than one responsibility.

Review and split by meaningful UI/business concepts.

A reasonable direction could be:

```text
features/sales/work-pane/
  WorkPaneHeader.tsx
  SalesRfqSummary.tsx
  SalesLifecycleActions.tsx
  SalesAmendmentSection.tsx
  SalesContactOwnerSection.tsx
  SalesMemoSection.tsx
```

Do not follow this structure mechanically.

Small sections that are tightly coupled can remain together.

The goal is that changing Memo behavior should not require reading lifecycle/action rendering, and changing Contact Owner UI should not require navigating quote-summary implementation.

---

# 8. Sales operation UI should receive intent-level callbacks

As with Trader, presentation-oriented Sales components should ideally not know generic execution/reconciliation machinery.

For example, a lifecycle section should prefer callbacks such as:

```ts
onPresent()
onHit()
onAway()
onCancel()
```

or a cohesive typed capability contract,

rather than needing to understand:

- reload policy
- paused-mode patch policy
- generic `run`
- error handling
- API request construction

Keep those mechanics in Screen/controller/Workspace layers.

---

# 9. SalesWorkspace — keep the composition root, reduce API adapter expansion

`SalesWorkspace` has the same structural issue as TraderWorkspace.

It is appropriate for the Workspace to:

- own RTK Query hooks
- bind backend operations to frontend feature contracts
- own the current visible snapshot
- mediate remote refresh state
- render the Screen

It is not ideal for the `<SalesScreen ... />` JSX itself to contain dozens of request adapters.

Current examples include inline bindings for:

- client/security search
- defaults resolution
- create/update/confirm draft
- lifecycle operations
- correction
- Contact Owner
- memo
- amendment operations
- bulk
- grid config

Refactor these into named, typed capability groups before JSX.

Candidate groups:

```text
draft
lifecycle
amendment
contactOwner
memo
bulk
lookup
gridLayout
refresh
```

For example:

```ts
interface SalesDraftActions {
  create: ...
  update: ...
  confirmNew: ...
  confirmDraft: ...
  discard: ...
}

interface SalesAmendmentActions {
  save: ...
  confirm: ...
  discard: ...
}
```

Do not create one giant `actions` object.

Do not create one tiny hook per endpoint.

---

# 10. SalesScreen Props — group cohesive capabilities

Review `SalesScreenProps` using the same rule as Trader.

Group callbacks where they form a real feature capability.

For example:

```ts
interface SalesScreenProps {
  ...

  draft: SalesDraftActions
  lifecycle: SalesLifecycleActions
  amendment: SalesAmendmentActions
  contactOwner: SalesContactOwnerActions
  memo: SalesMemoActions
  bulk: SalesBulkActions
  lookup: SalesLookupActions

  ...
}
```

This is preferable only when the grouped contract is semantically meaningful.

Keep simple view-state values explicit.

Do not optimize solely for fewer props.

---

# 11. Workspace callback expansion: what is normal and what is not

It is normal for a top-level React container/composition root to wire dependencies into a Screen.

Therefore:

- do not hide all dependencies behind service locators
- do not move RTK Query hooks into presentation components merely to reduce props
- do not introduce React Context just to avoid explicit props
- do not introduce a generic command bus

The maintainability issue is specifically:

> dozens of inline request-construction lambdas inside JSX make composition difficult to scan.

The preferred result is:

```text
Workspace
  1. read queries/mutations
  2. construct named feature capabilities/adapters
  3. own snapshot/refresh state
  4. render Screen with readable contracts
```

This keeps dependency injection explicit while reducing callback noise.

---

# 12. Live / Paused refresh state — inspect but do not over-generalize yet

SalesWorkspace and TraderWorkspace still contain highly similar logic for:

- `visibleRfqs`
- `refreshMode`
- `pendingUpdateCount`
- `protectedState`
- `deferredUpdate`
- `observedRemoteVersion`
- remote-change effects
- Live / Paused transitions
- catch-up

This is a real duplication candidate.

However, their catch-up semantics are not identical:

- Sales also refreshes Recent Revisions
- Trader increments refresh generation to invalidate calculation responses

The later state/orchestration cleanup will address this more directly.

For this follow-up:

- you may extract **feature-local** refresh controllers if needed to simplify each Workspace
- do not force Sales and Trader into one generic shared hook unless the common state machine can be extracted without hiding feature-specific catch-up semantics
- if shared extraction would require many callbacks/options/flags, defer it

Do not change behavior.

---

# 13. Common presentation components

01b already introduced shared bulk-result presentation.

Preserve it if it remains clean.

Re-evaluate the Live / Paused toolbar presentation.

Sales and Trader both display:

- mode selector
- pending count
- deferred-update indicator
- manual refresh

If the presentation is genuinely equivalent, a small shared presentational component such as:

```tsx
<RefreshModeControl ... />
```

is reasonable.

Keep the actual refresh state machine outside the component.

Do not build a generic toolbar framework.

---

# 14. Review repeated small feature types

Types such as:

```ts
type UserOption = { userId: string; name: string }
```

appear in multiple feature files.

Do not create a global `common/types.ts` merely for one small type.

If several files within one feature genuinely share the concept, a feature-local named contract is acceptable.

If the same concept is truly application-wide and semantically identical, place it in a clearly named cross-cutting module.

Prefer semantic clarity over deduplication.

---

# 15. Import / source hygiene

While reviewing the refactored files:

- keep import declarations together at the top of the module
- remove stray imports introduced below declarations
- remove dead imports/exports
- avoid unnecessary barrel files
- keep feature-local implementation details non-exported
- preserve explicit named boundary types

Do not perform unrelated stylistic churn.

---

# 16. Function-level readability

Review remaining large handlers/controllers.

Extract a function when it mixes several meaningful phases such as:

- validation
- request construction
- execution
- local reconciliation
- UI reset
- error handling

Prefer high-level workflows that can be understood from named steps.

Do not replace straightforward code with chains of trivial wrappers.

---

# 17. JSX readability

Do not flatten natural visual hierarchy.

Refactor when JSX still contains too much interaction logic, such as:

- long inline mutation adapters
- complex state transitions
- deeply nested ternaries
- request construction
- reconciliation behavior
- repeated multi-line callback bodies

Prefer named callbacks/controllers where they represent real intent.

Do not create a component for every `<div>`.

---

# 18. Test maintainability

Preserve all existing behavior coverage from 01a/01b.

If responsibilities are moved into hooks/controllers/modules:

- test pure logic directly where useful
- keep integration/screen tests for observable behavior
- do not rewrite tests solely to mirror implementation structure

Preserve tests for:

- grid selection behavior
- Live / Paused semantics
- protected editing state
- bulk partial success
- Trader calculation locking
- stale calculation response suppression
- timeout recovery
- scratch-pricer provenance
- Post Process staged edits/navigation protection

---

# 19. Blank-line and formatting rules

Keep the 01b formatting strategy:

```text
Prettier
  -> syntax/layout formatting

ESLint Stylistic
  -> obvious structural blank lines

Human/Codex judgment
  -> semantic grouping
```

Keep `@stylistic/padding-line-between-statements` conservative.

Do not add a large stylistic ruleset.

Use blank lines between meaningful phases/groups, not every one-line statement.

---

# 20. Do not perform later behavior/API tasks here

Do not:

- change business rules
- change authorization behavior
- change API endpoint contracts
- decompose `services/api.ts` yet
- adopt generated OpenAPI schema types yet
- replace Sales paused-state prediction with authoritative mutation responses yet
- change Live / Paused semantics
- fix known product/spec discrepancies
- perform broad CSS redesign
- change backend code
- modify `src/generated/api-schema.ts`

Known product/spec questions remain separate:

1. Save Draft minimum-field behavior
2. Contact Owner filtering for Sales bulk draft operations
3. Contact Owner filtering for Sales amendment editing

---

# 21. Verification

Run at minimum:

```bash
npm test
npm run build
npm run lint
```

Also run the repository's frontend quality/format command if available.

Inspect lint autofix output.

If a test fails because behavior changed, restore the original behavior.

GitHub currently has no workflow/status checks proving these frontend commands for the reviewed commits, so local verification in this task is important.

---

# Completion criteria

The original 01 cleanup goal is complete when the current frontend satisfies the following:

## Trader

- `TraderScreen` primarily composes coherent controllers/UI regions rather than owning every feature interaction directly
- search interaction state is localized
- scratch-pricer interaction state is localized
- working-quote calculation coordination remains coherent
- multi-grid layout coordination is understandable
- operation concepts are separated meaningfully
- operation UI does not need generic mutation/reconciliation mechanics where intent callbacks are sufficient
- `TraderWorkspace` remains the composition root without a giant inline JSX adapter implementation
- Trader capabilities have clear named contracts

## Sales

- `SalesScreen` no longer directly owns all editor/lifecycle/bulk/memo/context-menu/keyboard concerns when they can be coherently separated
- New/Draft editing has a readable interaction boundary
- bulk workflow has a readable interaction boundary
- lifecycle execution/reconciliation is localized
- `SalesWorkPane` is not a collection of unrelated feature concepts in one component/file
- `SalesWorkspace` remains the composition root without a giant inline JSX adapter implementation
- Sales capabilities have clear named contracts

## General

- exported object-shaped boundaries generally use named interfaces
- unions/aliases use `type` naturally
- obvious locals still rely on inference
- dependency direction is understandable
- shared abstractions represent truly shared concepts
- no generic framework/service-locator/context abstraction is introduced merely to reduce prop count
- no intended behavior is removed
- tests/build/lint/quality pass

---

# Deliverable

Make the changes directly in the repository.

At the end, provide a concise review summary containing:

1. residual problems found after 01a/01b
2. which 01a/01b concerns were already resolved and therefore left untouched
3. responsibilities extracted from `TraderScreen`
4. changes to `TraderWorkspace` composition/API binding
5. responsibilities extracted from `SalesScreen`
6. changes to `SalesWorkspace` composition/API binding
7. operation/work-pane concept splits performed
8. capability/type contracts introduced
9. any complexity deliberately preserved and why
10. verification commands and results

If a reviewed area is already adequately designed, explicitly leave it unchanged rather than refactoring for activity's sake.
