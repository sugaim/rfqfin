# Codex Instruction — Rfq.Web Implementation Maintainability Pass

## Position of this task

This task runs **after** the initial Rfq.Web structure/test cleanup.

The previous refactor should already have:

- moved frontend tests under `src/test`
- split the large root frontend test file
- decomposed the largest Sales/Trader screen files to some degree
- preserved application behavior

This task is a second frontend cleanup pass focused specifically on **implementation-level maintainability**.

Do not redesign the application.

Do not change business behavior.

Do not change API contracts.

Do not perform the later state/orchestration refactor or API-contract refactor in this task.

---

# Read this first

Before changing code, read:

- `docs/design.md`

Treat `docs/design.md` as the single canonical design document.

Preserve in particular:

- dense desktop/grid-first UX
- keyboard efficiency
- Sales / Trader / Post Process distinction
- Live / Paused behavior
- protection of actively edited operator state
- per-case partial success for bulk operations
- SSE as a wake-up signal rather than authoritative state
- frontend not being the business authority
- frontend-owned grid layout persistence
- feature-first source organization

If an implementation looks more complicated than expected, first check whether that complexity exists to preserve one of these behaviors before simplifying it.

---

# Goal

Improve the readability and local maintainability of `src/Rfq.Web` after the first structural refactor.

Focus on:

1. excessive JSX / callback nesting
2. components that still own too many responsibilities
3. large helper functions that should be separated
4. feature-local abstractions that make intent clearer
5. genuinely shared components where the shared concept is real
6. excessive prop surfaces
7. explicit and readable type boundaries
8. logical whitespace and minimal intent comments
9. conservative formatter/linter automation for obvious blank-line structure

The objective is not to minimize lines of code.

The objective is to make the code easier for a human maintainer to navigate, understand, and modify safely.

---

# 1. Review the result of the previous refactor first

Do not immediately create more files.

First inspect the current state of:

- Sales feature
- Trader feature
- Post Process feature
- shared grid code
- app shell
- frontend test structure

Identify whether the first refactor actually reduced reasoning complexity, or merely moved large blocks into different files.

Look specifically for:

- components still several hundred lines long with multiple visual/behavioral sections
- files that now contain a large number of unrelated callbacks
- props objects that simply moved a giant flat dependency list elsewhere
- deeply nested conditional JSX
- large inline `map(...)`, ternary, callback, or state-update expressions
- column definitions tightly mixed with unrelated application behavior
- duplicated interaction patterns that represent the same concept

Make changes only where there is a clear maintainability benefit.

---

# 2. Reduce semantic nesting

Do not chase indentation mechanically.

Deep JSX is acceptable when it reflects simple visual hierarchy.

Refactor when nesting makes it difficult to answer questions such as:

- What state controls this section?
- Which handler owns this operation?
- Which condition enables this action?
- What behavior belongs to this visual region?
- Which code is grid configuration versus workflow logic?

Examples of useful extraction boundaries include:

- a self-contained form
- a toolbar
- a search section
- a confirmation section
- an operations section
- a bulk-action section
- a result-summary section
- a grid definition
- a logically independent interaction controller

Prefer visually meaningful component boundaries over arbitrary small components.

Avoid extracting components that only wrap a few lines and add no semantic name.

---

# 3. Explicit type-boundary policy

Prefer **explicit named types at module and component boundaries**.

This repository should make the contract of a component/function visible without requiring the reader to infer it from the implementation.

## Prefer explicit typing for

- exported functions
- React component props
- exported hooks
- callbacks crossing component/module boundaries
- feature-level action contracts
- non-trivial state shapes
- API-facing values
- shared feature contracts

For React components, prefer this:

```ts
interface TraderPricerPaneProps {
  selected?: TraderRfq
  scratch: ScratchState
  setScratch: Dispatch<SetStateAction<ScratchState>>
  setScratchIdentity: (
    key: 'securityId' | 'notional' | 'settlementDate',
    value: string | number | null,
  ) => void
  result: CalculatedQuotePayload | null
  provenance: PricerProvenance | null
  sourceRow?: TraderRfq
  canApply: boolean
  busy: boolean
  onLoad: () => void
  onCalculate: () => void
  onApply: () => void
  onClear: () => void
}

export function TraderPricerPane({
  selected,
  scratch,
  setScratch,
  setScratchIdentity,
  result,
  provenance,
  sourceRow,
  canApply,
  busy,
  onLoad,
  onCalculate,
  onApply,
  onClear,
}: TraderPricerPaneProps): ReactElement {
  ...
}
```

rather than an anonymous inline object type attached to the destructured parameter.

Named `Props` types/interfaces should normally live immediately above the component unless they are part of a broader feature contract.

## Return types

Add explicit return types to exported functions and exported React components where practical.

Do not force explicit return types onto every trivial local callback.

## Local inference is allowed

Do **not** mechanically annotate obvious local values such as:

```ts
const quoted = selected.quoteStatus === 'Quoted'
const count = rows.length
const title = `Case ${row.caseId}`
```

Avoid noise such as:

```ts
const quoted: boolean = ...
const count: number = ...
const title: string = ...
```

The intended rule is:

> Explicit at boundaries; inference for obvious local implementation details.

## Avoid parent-component type borrowing

Do not make a child component depend on the parent Screen's Props type merely to reuse callback signatures.

Avoid patterns such as:

```ts
onPickUp: TraderScreenProps['onPickUp']
```

inside a child component.

Prefer either:

1. a dedicated feature-level action contract, when several components genuinely share it, or
2. a named child `Props` interface with the callback signatures written directly.

For example:

```ts
interface TraderOperationsPaneProps {
  onPickUp: (...)
  onRelease: (...)
  ...
}
```

or, where genuinely shared:

```ts
interface TraderOwnershipActions {
  onPickUp: (...)
  onRelease: (...)
  onAssign: (...)
  onTakeOver: (...)
}
```

Do not create shared action interfaces merely to reduce typing repetition.

Use them only when they represent a real feature boundary.

---

# 4. Sales maintainability review

Review the current Sales implementation after the first refactor.

Pay particular attention to whether these concerns are clearly separated:

- RFQ grid
- selection
- New/Draft editing
- security/default resolution
- Work Pane
- lifecycle actions
- amendment actions
- bulk actions
- Recent Revisions
- context-menu construction
- keyboard interaction
- result presentation
- grid layout persistence

If one component still owns several of these responsibilities, split further.

Prefer feature-local files under `features/sales`.

Use `.ts` for pure logic when React is not required.

Do not change Sales behavior.

## Important restriction

Do not yet redesign paused-mode authoritative state handling.

Preserve current semantics of:

- local paused snapshot patching
- `applyRowAction`
- `reflectConfirmedAmendment`

A later task will replace frontend-predicted state with authoritative mutation responses.

---

# 5. Trader maintainability review

Review the current Trader implementation after the first refactor.

Pay particular attention to these separate concerns:

- active RFQ grid
- quote cell editing
- working quote calculation coordination
- stale-result suppression
- search
- confirmation
- operations
- scratch pricer
- result presentation
- keyboard interaction
- grid layout persistence

The working quote calculation coordination is especially important.

The logic around concepts such as:

- request sequence
- per-Case request tracking
- in-flight Cases
- refresh generation
- timeout
- calculation state
- stale response suppression

should be readable as one coherent responsibility.

If it is still spread through a large component, isolate it into a feature-local hook/controller/module.

Do not change its behavior.

A short comment explaining **why** stale responses must be ignored is appropriate.

Do not add comments that restate obvious code.

---

# 6. Review large subcomponents too

Do not stop once the top-level Screen files become smaller.

Inspect extracted components such as operations panes, lifecycle panes, search panels, editors, and confirmation views.

If an extracted component still contains several independent visual/behavioral sections and remains difficult to reason about, split it further along meaningful boundaries.

For example, an operations pane may naturally contain:

- ownership actions
- quote actions
- lifecycle actions
- contact-owner actions
- bulk actions

Do not split these mechanically if the resulting files would be trivial.

Use the visual and behavioral sections already present in the UI as the preferred boundaries.

---

# 7. Review column-definition placement

Large AG Grid column definitions should not force maintainers to navigate unrelated workflow code.

If Sales or Trader column definitions are still embedded inside large screen/controller components, consider moving them into feature-local modules or hooks.

Possible forms include:

```ts
buildSalesColumns(context)
```

or:

```ts
useSalesColumns(context)
```

Choose based on whether React hooks/state are actually required.

Do not create a giant context object merely to hide 20 unrelated arguments.

If the dependency surface is large, group dependencies only by real responsibility, such as:

- grid state
- lifecycle actions
- quote actions
- amendment actions

The grouping should clarify the feature boundary rather than conceal dependencies.

Give exported column builders/hooks explicit named input and return types.

---

# 8. Shared component review

Do not create generic abstractions just because Sales and Trader look similar.

Shared code is justified when the **concept itself** is shared.

Review at least these candidates:

## Live / Paused refresh control

Sales and Trader both expose the same conceptual controls:

- Live / Paused mode
- pending update count
- deferred update indication
- manual refresh

If the UI and behavior are still materially equivalent after the first refactor, extract a small shared presentation component.

For example:

```tsx
<RefreshModeControl
  mode={...}
  pendingCount={...}
  deferred={...}
  onModeChange={...}
  onRefresh={...}
/>
```

Keep the actual refresh state machine outside this component.

Do not create a generic toolbar framework.

## Bulk partial-success result presentation

Sales, Trader, and Post Process all display results based on the same general `BulkItemResult` contract:

- Succeeded
- Skipped
- Failed
- optional code
- optional message
- expandable detail

If the presentation remains duplicated, extract a small shared component for the common result presentation.

Feature-specific labels or surrounding controls may remain feature-local.

## Do not over-share trivial duplication

Do not create shared modules merely for things such as:

```ts
const million = 1_000_000
```

or tiny local types unless there is meaningful shared behavior around them.

Prefer small feature-local duplication over an unclear `shared`, `common`, or `utils` bucket.

---

# 9. Function separation

Review large functions and handlers.

Separate a function when doing so makes a meaningful phase or responsibility explicit.

Good candidates include functions that currently combine several of:

- validation
- DTO construction
- mutation execution
- local state reconciliation
- error handling
- UI state reset

Prefer names that expose intent.

Do not decompose a straightforward 10-line function into multiple trivial wrappers.

A useful test is:

> Can a maintainer understand the high-level workflow by reading the function names without reading every implementation detail?

When extracting non-trivial functions across module boundaries, give their inputs and outputs explicit named types.

---

# 10. Props and dependency surfaces

Review large component prop interfaces.

Do not replace a flat 40-prop interface with one opaque `actions` object merely to reduce the visible count.

Group props only when there is a real conceptual boundary.

Examples of acceptable groupings:

```ts
refresh
draftActions
lifecycleActions
amendmentActions
workingQuoteActions
gridLayout
```

Avoid nested structures that make call sites harder to understand.

Prefer component extraction first; prop grouping should follow natural component responsibility.

Use named interfaces/types for these contracts.

Do not hide dependency complexity behind `Record<string, unknown>`, broad callback bags, or weakly typed generic containers.

---

# 11. Comments and whitespace

Readability matters for human maintainers.

Do not document every function.

Add comments only when they explain:

- non-obvious intent
- concurrency protection
- stale-state protection
- a deliberate UX constraint
- a caveat that a future maintainer could easily break
- why an apparently simpler implementation is incorrect

Do not add comments that merely restate the next statement.

## Blank lines

Use blank lines to separate meaningful logical blocks.

Do not insert a blank line after every one-line statement.

Good examples of grouping include:

```ts
const selected = ...
const mode = ...
const isProtected = ...

useEffect(() => {
  ...
}, [...])

const startNew = () => {
  ...
}

const selectRow = () => {
  ...
}
```

Think in terms of semantic groups such as:

- derived state
- effects
- event handlers
- validation
- API execution
- error handling
- render preparation

rather than line-by-line spacing.

---

# 12. Add conservative automatic blank-line enforcement

Prettier is already responsible for syntax formatting, but it does not infer semantic grouping.

Add `@stylistic/eslint-plugin` if it is not already present.

Use `@stylistic/padding-line-between-statements` only for **obvious, low-risk structural spacing** that can be automatically fixed.

Keep the rules conservative.

Good candidates include:

- always require a blank line before `return`
- keep adjacent type/interface declarations together where appropriate
- separate declaration groups from executable statements where the rule can be expressed reliably

Do not attempt to encode semantic business grouping into ESLint.

Do not add a large or clever formatting rule set.

The objective is:

```text
Prettier
  -> syntax/layout formatting

ESLint Stylistic
  -> obvious structural blank lines

Human/Codex judgment
  -> semantic grouping
```

Use the Stylistic version of the rule rather than deprecated ESLint-core stylistic rules.

Ensure the configured rules work with the current ESLint flat config.

Run autofix where appropriate and inspect the result.

If a rule produces noisy or unnatural whitespace across the existing codebase, remove or relax the rule rather than forcing the code to conform.

---

# 13. Public/export surface

Review feature-local exports while moving/refactoring code.

Do not mechanically use:

> unused outside file => private

Instead ask whether the symbol is part of the intended module boundary.

Prefer non-exported implementation details where no external feature/module consumer needs them.

Avoid unnecessary barrel files if they only obscure where a symbol is defined.

For exported symbols, prefer explicit named parameter and return types where practical.

---

# 14. Do not do these things in this task

Do not:

- change business rules
- change authorization behavior
- change API endpoint contracts
- adopt generated OpenAPI types yet
- reorganize `services/api.ts` yet
- change Live / Paused state-machine semantics
- replace Sales paused local patches with server-authoritative responses yet
- perform broad CSS architecture changes
- redesign the UI
- create a generic application framework
- create catch-all `shared`, `common`, or `utils` folders
- remove behavior because it appears complex
- modify `src/generated/api-schema.ts`

---

# 15. Known behavior/spec questions — preserve them for now

Do not fix these as part of this maintainability pass:

1. Save Draft validation may be stricter than the design wording around minimum Client + Security.
2. Sales bulk draft actions may not filter by Contact Owner at the UI level.
3. Sales inline amendment editability may not filter by Contact Owner at the UI level.

These belong to a later behavior/spec pass.

---

# Verification

Run at minimum:

```bash
npm test
npm run build
npm run lint
```

Also run the repository's frontend quality/format check if available.

Run ESLint autofix after adding the Stylistic blank-line rules, then inspect the diff.

Do not accept a formatting rule that creates noisy whitespace.

If a test fails because behavior changed, restore the original behavior.

---

# Completion criteria

This task is complete when:

- top-level Screen decomposition from the previous task has been reviewed rather than assumed correct
- remaining large subcomponents have coherent single responsibilities
- excessive semantic nesting has been reduced
- large workflow functions are separated where it improves reasoning
- column definitions are placed where they can be maintained independently of unrelated workflow code
- genuinely shared UI concepts are reused without introducing generic frameworks
- props/dependency surfaces are understandable
- exported functions/components use clear named type boundaries where practical
- child components do not borrow parent Screen prop types merely for callback signatures
- obvious local values are still allowed to use TypeScript inference
- non-obvious concurrency/state-protection intent is documented minimally
- logical blank lines improve scanability
- conservative ESLint Stylistic blank-line rules are configured and autofixable
- tests/build/lint/quality checks pass
- observable application behavior remains unchanged

---

# Deliverable

Make the changes directly in the repository.

At the end, provide a concise summary containing:

1. maintainability issues found after the first refactor
2. additional component/function boundaries introduced
3. shared components introduced, with justification
4. prop/dependency cleanup performed
5. explicit type-boundary cleanup performed
6. ESLint Stylistic rules added
7. deliberately preserved complex behavior
8. verification commands and results

Do not include unrelated redesign proposals in the implementation commit.
