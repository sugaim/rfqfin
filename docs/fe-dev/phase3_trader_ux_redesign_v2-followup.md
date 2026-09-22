# Trader Workstation Follow-up

## Baseline

Review/follow-up target:

- `9b9f2a6bcb788bf9a896b66812b5fac0ba20c3c4` — `feat: extend trader quote contracts`
- `d0494162ecde337503f473988254051004cdac93` — `feat: redesign trader workstation`
- `d35aeceee73c1f169a9ede34cdf65a401eec9f45` — formatter/quality-only follow-up

Assume this application is not yet released. Persisted quote payload backward compatibility is therefore **not** part of this follow-up.

Do not redesign the Trader screen. Keep the current Trader UX and architecture unless required for the fixes below.

---

## 1. Do not globally block unrelated Trader actions during a per-Case calculation

### Problem

The Trader UX is intended to allow only one Working Quote calculation in flight **per Case**, while unrelated RFQs remain operable.

The inline calculation implementation already tracks this locally through mechanisms such as:

- `inFlightCases`
- `calcStates`

However, `TraderWorkspace` currently includes calculation-related RTK mutation state in the aggregate `isMutating` flag, and `TraderScreen` uses that aggregate flag to disable unrelated operations such as:

- `Pick`
- `Confirm`
- `Release`
- `Assign`
- `Take Over`
- `Withdraw`
- other Operations-pane actions

As a result, calculating Case A can temporarily disable actions for Case B.

### Required behavior

During a calculation for Case A:

- quote-edit cells for Case A must remain locked against another simultaneous calculation;
- unrelated Case B must remain selectable and operable;
- Search must remain usable;
- row selection must remain usable;
- unrelated ownership/lifecycle/quote-confirmation actions must not be disabled solely because Case A is calculating.

Do not replace the existing per-Case calculation lock with a global lock.

### Implementation guidance

Refine the meaning of the global busy/mutation flag.

Calculation-related mutations that are already protected per Case should not cause unrelated Trader controls to become globally disabled.

At minimum inspect:

- `useCalculateWorkingQuoteMutation`
- `useUpdateManualWorkingQuoteMutation`

and any other Working Quote edit operation that should be Case-local rather than screen-global.

Do **not** remove protection against duplicate operations on the same Case.

Keep stronger/global busy behavior only where it is actually needed for the specific operation being executed.

### Tests

Add/adjust frontend tests proving that:

1. while Case A calculation is pending, Case A cannot start a second calculation;
2. while Case A calculation is pending, an unrelated Case B remains operable;
3. unrelated top-level/Operations actions are not globally disabled only because Case A is calculating;
4. existing timeout/failure/stale-response protections still work.

---

## 2. Restore confirmation semantics for Pick Up of another Trader's unowned RFQ

### Existing rule

The established ownership UX is:

```text
self-assigned + unowned
  -> Pick Up without additional confirmation

other-assigned + unowned
  -> explicit confirmation required

other-owned
  -> use Take Over, with strong confirmation
```

The backend intentionally enforces this through the `confirmed` argument of `PickUpRfq`.

### Problem

The redesigned Operations pane currently calls Pick Up approximately as:

```ts
onPickUp(
  selected,
  selected.assignedTraderId !== currentUserId,
)
```

This sends `confirmed = true` for another Trader's unowned RFQ **without first showing the user a confirmation**.

That bypasses the intended UX meaning of the backend confirmation flag.

The old Trader UI had an explicit confirmation for this case.

### Required single-item behavior

For single-item `Pick Up` in Operations:

#### Self-assigned + unowned

Execute directly:

```text
confirmed = false
```

No extra modal/popconfirm is required.

#### Other-assigned + unowned

Show an explicit confirmation first.

Only after confirmation execute:

```text
confirmed = true
```

A compact `Popconfirm` is sufficient. Do not introduce a new large modal.

#### Owned

Do not offer Pick Up. Existing Take Over behavior remains separate.

---

## 3. Make multi-selection Pick behavior consistent

The dedicated toolbar command:

```text
Pick (N)
```

is special and should continue to target only:

```text
assignedTraderId == currentUserId
AND unowned
AND eligible/open
```

It therefore does not require confirmation.

Do not change that behavior.

### Operations-pane multi-select Pick

The Operations pane currently exposes a generic multi-select `Bulk Pick`.

Do not send a mixed set with `confirmed = false` and rely on backend failures for other-assigned rows.

Use one of these simple approaches:

#### Preferred

Before executing, classify selected eligible unowned rows:

```text
self-assigned rows
other-assigned rows
```

If any other-assigned row exists, show one summary confirmation for the bulk operation.

Then send per-item `confirmed` consistently:

```text
self-assigned row   -> false
other-assigned row  -> true
```

This matches the existing authorization contract.

Alternatively, if keeping generic multi-select Pick adds unnecessary UX complexity, it is acceptable to narrow the Operations-pane bulk Pick to self-assigned/unowned rows only, as long as behavior is explicit and tests reflect it.

Do not change Take Over semantics.

### Tests

Add/adjust tests for:

- self-assigned/unowned single Pick -> no confirmation, `confirmed=false`;
- other-assigned/unowned single Pick -> confirmation shown, then `confirmed=true`;
- other-owned -> Pick unavailable; Take Over remains the path;
- toolbar `Pick (N)` only targets self-assigned/unowned rows and needs no confirmation;
- multi-select Pick does not silently bypass the confirmation rule and does not knowingly submit other-assigned rows with `confirmed=false`.

---

## Non-goals

Do not address persisted `calculated-v1` backward compatibility in this follow-up.

Do not redesign:

- RFQ lifecycle;
- ownership Domain rules;
- Quote Mode semantics;
- Paused-mode reconciliation;
- Pricer provenance;
- Search layout;
- Result Bar;
- Daily Review / Post Process.

Do not broaden this follow-up into unrelated refactoring.

---

## Validation

Run the existing project validation plus the focused Trader tests.

At minimum:

```bash
cd src/Rfq.Web
npm test -- --run
npm run build
```

If repository quality commands are now authoritative, also run the relevant quality check:

```bash
npm run quality
```

Run backend tests if any backend contract or behavior is changed. Ideally these fixes remain primarily frontend orchestration/UX changes because the backend authorization rules are already correct.

---

## Expected result

After this follow-up:

1. calculation remains one-in-flight-per-Case without freezing unrelated RFQs;
2. Pick Up confirmation semantics again match the established authorization/UX contract;
3. dedicated `Pick (N)` remains fast and selection-independent for self-assigned/unowned work;
4. no unrelated Trader UX behavior changes.
