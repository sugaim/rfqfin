Apply a focused follow-up to commit `ac27b5c22988dbf3906275b32cb70aee38e3f574`.

Do not redesign Post Process. Preserve the current Business Date semantics, staged-change model, same-Case atomic commit behavior, visual semantics, and existing Sales/Trader behavior.

1. Fix Post Process query reconciliation across preset/scope caches.

Pending changes intentionally survive:

`Today <-> Unclosed`

and:

`Mine <-> All permitted`

A commit can therefore contain Cases that are not present in the currently displayed query.

Currently the commit path refreshes only the currently active `getPostProcess({ preset, scope })` query. This can leave another previously visited Post Process query cache stale.

For example:

`Unclosed -> stage Away -> switch to Today -> Commit -> switch back to Unclosed`

must not show the successfully closed Case from a stale cached `Unclosed` result.

Make successful Post Process mutations invalidate/reconcile all Post Process worklist query variants, not only the currently displayed query.

Prefer normal RTK Query cache invalidation semantics rather than manually enumerating the four preset/scope combinations.

After commit:

* the active Post Process query must reconcile with authoritative backend state;
* previously cached inactive Post Process preset/scope variants must not later surface stale rows;
* failed Cases must remain pending as they do now;
* successful Cases must be removed from pending state as they are now.

Add a frontend test covering a commit after a preset/scope switch and subsequent return to the previously cached view.

2. Protect pending Post Process changes from SPA navigation.

The existing `beforeunload` handling protects browser reload/close, but AppShell navigation uses React Router SPA navigation and does not trigger `beforeunload`.

When Post Process has pending changes and the operator attempts to navigate to another application route such as Sales or Trader:

* warn that uncommitted Post Process changes will be discarded;
* cancel keeps the operator on Post Process with pending state unchanged;
* confirm allows navigation and discards the component-local pending state naturally.

Use the React Router navigation-blocking mechanism supported by the current repository version. Do not replace SPA navigation with hard reloads.

Keep the existing browser reload/close warning as well.

Add a test for internal route navigation with pending changes.

3. Do not reuse the historical Correction Reason as the new staged Correction Reason.

Current behavior falls back to `lastCorrectionReason` while a new correction is staged but no new reason has yet been entered.

Keep these semantics distinct:

No staged correction:
the Correction Reason cell may display `lastCorrectionReason` read-only.

Staged `CorrectToHit` / `CorrectToAway`:
the editable value represents the new correction reason and must start empty unless the operator has explicitly entered a new staged reason.

Do not silently copy or prefill the previous audit reason into the new correction.

The existing validation remains:

* null / empty / whitespace is invalid;
* Confirm Changes remains disabled while any staged correction has no non-empty new reason;
* trim the new reason before commit.

Add a frontend test where a row already has `lastCorrectionReason`, then stage another correction and verify that the new editable/staged reason is initially empty and cannot be committed until explicitly entered.

Keep this follow-up narrowly scoped. Do not implement Settings, theme switching, or general color-token refactoring in this change.
