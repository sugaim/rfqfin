# Post Process v1 — Implementation Instruction

## 0. Baseline and intent

Implement the first usable **Post Process** screen for the RFQ application.

Semantic baseline reviewed:

- `d35aeceee73c1f169a9ede34cdf65a401eec9f45` — formatting / quality setup
- prior Trader redesign:
  - `9b9f2a6bcb788bf9a896b66812b5fac0ba20c3c4`
  - `d0494162ecde337503f473988254051004cdac93`

A separate Trader follow-up may land before this work. Do not undo unrelated Trader fixes.

The application is still unreleased. Do **not** spend effort on backward compatibility for development-only persisted data. Normal migrations/reset-dev behavior is acceptable.

This phase replaces the current lightweight `Daily Review` concept with a real **Post Process** worklist.

The purpose is operational cleanup and end-of-day/post-trade follow-up, not intraday quoting.

---

# 1. Product purpose

Post Process v1 must support:

1. finding RFQs that were left unclosed;
2. closing them as `Hit`, `Away`, or `Cancelled`;
3. correcting a same-Business-Date `Hit/Away` outcome;
4. updating the current user's own-side memo;
5. reviewing today's relevant RFQs;
6. letting permitted users/management inspect the worklist;
7. staging multiple changes locally, reviewing the diff, then committing them.

The screen is **not** another Trader blotter and is **not** another Sales RFQ-entry screen.

Quote values are context only.

---

# 2. Explicit non-goals

Do not implement in this phase:

- Booking creation;
- booking reconciliation workflow beyond visually inspecting quote context;
- ticket generation;
- statistics / analytics UI;
- Hit Ratio dashboards;
- client / trader / sales analytics;
- historical arbitrary-date editing;
- Reopen;
- quote calculation;
- quote editing;
- quote confirmation;
- Working Quote mode changes;
- new Desk Domain modeling;
- generic workflow engine;
- server-side Post Process drafts;
- event-sourcing redesign.

Do not reserve empty UI panels for Booking/Ticket/Statistics.

Statistics may later become another tab in Post Process after data has accumulated, but v1 is only the Worklist.

---

# 3. Replace Daily Review with Post Process

The current `DailyReviewWorkspace` is only a temporary placeholder:

- EOD summary;
- generic Past RFQ grid;
- recent Changes list.

Replace this user-facing surface with **Post Process**.

Prefer the user-facing name:

```text
Post Process
```

The route may be changed from the old Daily Review route to a Post Process route because the application is unreleased.

Remove obsolete placeholder UI rather than keeping both concepts side-by-side.

The old EOD summary API may be removed if it becomes unused. Do not preserve it merely for compatibility.

---

# 4. Main screen structure

Use one primary Grid.

Top toolbar:

```text
[ Today | Unclosed ]   [ Mine | All permitted ]        [ Confirm Changes (N) ] [ Refresh ]
```

The exact Ant Design controls may be compact `Segmented` / buttons as appropriate.

No detailed Sales / Trader / Contact Owner filter panel is required initially.

Manual Refresh is enough. Post Process does not need Trader-style Live/Pause event-driven operation.

---

# 5. Worklist presets

## 5.1 `Unclosed`

`Unclosed` contains RFQs whose lifecycle is currently:

```text
Active
Presented
```

across Business Dates.

Quote state does not change this definition.

Examples:

```text
Active + Requested   -> Unclosed
Active + Quoted      -> Unclosed
Presented            -> Unclosed
Hit                  -> not Unclosed
Away                 -> not Unclosed
Cancelled            -> not Unclosed
Draft                 -> excluded
```

Do not include Draft.

---

## 5.2 `Today`

`Today` is **not** based on local-calendar `00:00 -> 24:00` timestamp ranges.

It is based on persisted Business Date facts.

Draft is excluded regardless of date.

A non-Draft RFQ belongs to `Today` when at least one relevant Post Process business fact belongs to the current Business Date:

```text
business-created today
OR Hit/Away/Cancelled today
OR outcome-corrected today
```

Quote confirmation and memo edits alone do **not** make an older Case appear in `Today`.

Do not derive `Today` in React by combining timestamp searches.

Expose it as one backend-defined query/preset.

---

# 6. Business Date semantics

## 6.1 Business Date is authoritative business data

`IBusinessDateProvider.GetCurrentAsync()` is the authoritative source of the current Business Date.

Do not use `DeskDateBoundary` to determine Post Process Business Date membership.

A Business Date may have a non-midnight rollover.

Example only:

```text
Tokyo Business Date 2026-09-22
may correspond to:
2026-09-22 06:00 JST <= timestamp < 2026-09-23 06:00 JST
```

Post Process and Domain rules must not know or calculate that rollover.

They should consume an already-resolved `DateOnly` Business Date.

Keep both concepts:

```text
OccurredAt   = physical timestamp
BusinessDate = business-day ownership
```

---

## 6.2 Business creation date

Persist an immutable business creation date for a Case.

Use a clear name such as:

```text
CreatedBusinessDate
```

For Post Process semantics, **business creation occurs when the initial Draft becomes the first confirmed/Active RFQ**.

Persisting a Draft row itself does not make it an operational RFQ for Post Process.

Therefore:

```text
Draft creation       -> no Post Process population
Initial confirmation -> establish CreatedBusinessDate
```

Once established, it does not change.

Do not recalculate it later from `CreatedAt`.

---

## 6.3 Closed Business Date belongs in the Domain

Hit/Away closure needs Business Date as a Domain fact because Outcome Correction is restricted to the original close Business Date.

Extend closed lifecycle state conceptually to retain:

```text
ClosedQuoteId
ClosedBusinessDate
```

For example:

```text
HitRfq(
  CurrentRevisionId,
  ClosedQuoteId,
  ClosedBusinessDate
)

AwayRfq(
  CurrentRevisionId,
  ClosedQuoteId,
  ClosedBusinessDate
)
```

Both `CloseHit` and `CloseAway` must receive the current Business Date explicitly.

Do not let Domain code call infrastructure/time providers directly.

---

## 6.4 Cancel and Correction Business Date

Cancellation does not currently require a Domain rule based on its Business Date.

Do not add unnecessary Case fields merely to support querying.

Instead, persist Business Date on the relevant business event/fact used by the Post Process projection.

At minimum, Post Process needs Business Date attached to:

```text
ClosedHit
ClosedAway
Cancelled
OutcomeCorrected
```

Outcome correction may keep its correction Business Date in the event/projection rather than adding a mutable `CorrectedBusinessDate` field to `RfqCase`.

Avoid growing `RfqCase` into:

```text
CreatedBusinessDate
ClosedBusinessDate
CorrectedBusinessDate
LastActivityBusinessDate
...
```

only for query convenience.

Persist business facts; project query-friendly fields separately.

---

# 7. Outcome Correction rule

Outcome Correction remains Contact Owner-authorized under the existing authorization rules.

Add the Business Date rule:

```text
currentBusinessDate == closedRfq.ClosedBusinessDate
```

Otherwise reject the correction in Application/Domain logic.

This must not be a UI-only restriction.

Only:

```text
Hit  -> Away
Away -> Hit
```

are corrections.

No Reopen is part of Post Process v1.

---

## 7.1 Correction Reason is required

Post Process requires a non-empty `Correction Reason`.

The current correction API/application accepts nullable/optional reason. Tighten this rule.

Reject:

```text
null
""
"   "
```

Normalize by trimming before persistence.

Correction Reason is audit data and is distinct from Sales Memo / Trader Memo.

Do not silently treat the memo as the correction reason.

---

# 8. Visibility scope

Post Process visibility may later need broader organizational controls.

Do not implement a full organization/Desk permission model now.

However, do not scatter visibility clauses such as:

```text
WHERE SalesId == currentUser
```

through Post Process queries.

Introduce one thin Application-level seam, conceptually:

```text
IPostProcessVisibility
```

or an equivalent narrowly scoped policy.

Its purpose is to define which Cases are visible in:

```text
All permitted
```

The first implementation may be simple/permissive enough for the current application, but all visibility logic must remain behind this seam.

Do **not** introduce a new `Desk` Domain aggregate for this work.

Visibility and action authorization are separate:

```text
visible != editable
```

Existing lifecycle/memo authorization remains authoritative for actions.

---

# 9. `Mine` definition

`Mine` is intentionally simple.

A Case is mine when:

```text
SalesId == currentUserId
OR ContactOwnerId == currentUserId
OR AssignedTraderId == currentUserId
```

No role-dependent branching.

Do not redefine Mine differently for Sales and Trader.

Apply `Mine` after/within the permitted Post Process visibility scope.

---

# 10. Memo semantics

There are exactly these relevant communication/memo concepts:

```text
SalesAndTradingMessage
SalesMemo
TraderMemo
```

Do not invent a fourth Shared Memo.

## SalesAndTradingMessage

- shared Revision message;
- read-only in Post Process.

## SalesMemo / TraderMemo

Expose only the current user's own-side memo.

Conceptually:

```text
Sales role  -> My Memo = SalesMemo
Trader role -> My Memo = TraderMemo
```

Do not expose the opposite-side private memo.

Management gets no special “see both private memos” privilege in this phase.

Reuse existing memo authorization semantics.

If a development identity has multiple roles, keep the implementation minimal and deterministic; do not add a new dual-memo management UX.

---

# 11. Worklist read model

Create a dedicated Post Process read model/query.

Do not build the screen from the existing generic RFQ Search endpoint in React.

A row should provide enough authoritative data to render and stage changes without N+1 browser requests.

At minimum expose:

```text
CaseId

CreatedAt
CreatedBusinessDate

ClientId
ClientName

SecurityId
SecurityName / useful display fields

Notional
SettlementDate

ContactOwnerId
SalesId
AssignedTraderId

RfqStatus
CurrentVersion

SalesAndTradingMessage

MyMemo
MyMemoVersion

Price
FinalSimpleYield

optional useful quote context:
  Yield
  Ysc
  GSpread

ClosedBusinessDate when applicable

LastCorrectionReason when applicable

LastChangedBy
LastChangedAt
```

Include any hidden IDs/versions needed for optimistic concurrency.

The UI must not infer Domain state transitions from partial data.

---

# 12. Quote context in Post Process

Post Process needs quote values to identify/reconcile the business.

Default visible quote columns:

```text
Px
Final SY
```

Useful optional/default-hidden context may include:

```text
Yld
YSC
GSpd
```

These are read-only.

Do not allow:

- calculation driver edits;
- manual/calculated mode edits;
- quote confirmation;
- repricing.

Use the most relevant authoritative current/final quote projection already available for the Case.

For closed Cases, show the closed/final quote context.

For open Cases with a current confirmed quote, show that confirmed quote context.

Do not route Post Process through Trader calculation APIs.

---

# 13. Grid columns

Initial main Grid:

```text
Case
Time
Client
Security
Notl
Contact Owner
Sales
Trader
State
Px
Final SY
Message
My Memo
Correction Reason
Last Changed By
Last Changed At
Action
```

Additional quote fields such as:

```text
Yld
YSC
GSpd
```

may be present but default hidden.

Keep the screen dense and operational.

---

# 14. Row visual semantics

Use separate visual channels for current status and pending browser changes.

## Current lifecycle/status

### Unclosed

Give Active/Presented rows a light amber attention background.

The purpose of `Unclosed` is to make unfinished work visible.

### Hit / Away

Normal row background.

Distinguish outcome in the `State` cell:

```text
HIT  -> green semantic tag/text
AWAY -> red semantic tag/text
```

### Cancelled

Light gray / subdued.

Do not make the entire Hit/Away row strongly green/red.

---

## Pending local change

Any row with an uncommitted local modification gets a single consistent left-edge marker.

Example:

```text
| blue/neutral left line | row ...
```

Do not use a different pending color per action.

Pending marker is independent from current lifecycle background/status color.

---

# 15. Browser-only staging model

Post Process changes are not sent to the server immediately.

The workflow is:

```text
1. operator edits/stages rows
2. browser holds pending diff
3. Confirm Changes (N)
4. review summary
5. Commit
```

No server-side draft.

A row may have multiple staged changes simultaneously.

Example:

```text
Case 101
  Outcome: Active -> Away
  Memo: changed
```

Do not model row selection as pending state.

These concepts are separate:

```text
selection      = browsing / targeting
pending change = uncommitted business diff
```

Removing row selection must not discard its pending change.

---

# 16. Staged lifecycle actions

Action buttons are based on the currently confirmed server state.

## Active / Presented

Offer:

```text
Hit
Away
Cancel
```

Clicking only stages the desired outcome.

Do not call the API yet.

If another outcome is chosen before commit, replace the prior pending outcome for that Case.

Example:

```text
stage Hit
then stage Away
=> pending outcome is Away
```

## Hit

Offer:

```text
Correct to Away
```

## Away

Offer:

```text
Correct to Hit
```

## Cancelled

No lifecycle action in Post Process v1.

## Reopen

Not available in this screen.

The operator can use the normal Sales/Trader workflow if Reopen is needed.

---

# 17. Correction Reason editing

`Correction Reason` is enabled only when a Hit/Away correction is staged.

It is required before Confirm/Commit.

For rows without a staged correction:

- show existing last correction reason if useful/read-only;
- do not allow arbitrary editing of historical correction reason.

Do not enable Correction Reason for an ordinary first-time Hit/Away close.

---

# 18. Memo staging

`My Memo` is directly editable in the Grid.

Editing stages a memo diff locally.

Do not send memo mutation on each cell edit.

Memo staging may coexist with lifecycle staging on the same Case.

Example:

```text
Case 101:
  Away
  My Memo: "follow tomorrow"
```

is one pending Case change.

---

# 19. Pending changes across view switches

Pending browser changes should survive ordinary worklist scope changes:

```text
Today <-> Unclosed
Mine <-> All permitted
```

Do not silently discard pending changes simply because the row is temporarily outside the current filtered population.

Maintain pending state keyed by CaseId independently from the currently rendered rows.

---

# 20. Refresh / navigation with pending changes

Keep this simple.

## Manual Refresh

If there are no pending changes:

```text
refresh immediately
```

If there are pending changes:

```text
warn that uncommitted changes will be discarded
confirm -> discard pending + refresh
cancel  -> remain unchanged
```

Do not persist/recover pending changes across refresh.

## Navigate away / browser reload

Use the normal unsaved-changes warning where practical.

Do not build durable browser storage for pending Post Process edits.

---

# 21. Confirm Changes dialog

Toolbar button:

```text
Confirm Changes (N)
```

`N` is the number of Cases with pending changes, not the number of individual fields changed.

Disable when `N == 0`.

The confirmation surface shows **only changed Cases**.

Use a compact table such as:

```text
Case
Client
Security
Current State -> New State
Memo Change
Correction Reason
```

For memo:

- `Changed` is enough in the main table;
- full old/new text may be expandable if convenient;
- do not turn the dialog into large memo cards.

Do not show internal expected-version fields to the operator.

---

# 22. Commit transaction semantics

The central rule:

```text
same Case  -> atomic
different Cases -> independent
```

Example:

```text
Case 101: Hit + Memo          -> Success
Case 102: Away                -> Success
Case 103: Correction + Memo   -> VersionConflict
```

Case 101's lifecycle and memo changes must not partially commit.

However, Case 103 failing must not roll back Case 101/102.

Process each Case as its own transaction / Unit of Work.

Return one result per Case.

Reuse the existing bulk result style where practical:

```text
Succeeded
Skipped
Failed
```

with error code/message.

---

# 23. Do not compose current public use cases naively

Current use cases such as:

```text
CloseHitRfq
CloseAwayRfq
CorrectOutcome...
UpdateSalesMemo
UpdateTraderMemo
```

call `SaveChangesAsync()` internally.

Therefore this is **not** acceptable for a combined Post Process Case commit:

```text
await closeAway.ExecuteAsync(...)
await updateSalesMemo.ExecuteAsync(...)
```

because Close could commit before Memo fails.

Do not solve this with compensating transactions.

---

# 24. Refactor mutation preparation from commit

Refactor existing Application implementation so reusable internal operations can prepare mutations without committing.

Conceptually:

```text
CloseAwayOperation.ApplyAsync(...)
  -> load / authorize
  -> Domain transition
  -> repository update
  -> event record
  -> NO SaveChanges

UpdateSalesMemoOperation.ApplyAsync(...)
  -> authorize
  -> memo transition
  -> repository update
  -> NO SaveChanges
```

Existing public use cases remain purpose-specific:

```text
CloseAwayRfq.ExecuteAsync(...)
  -> CloseAwayOperation.ApplyAsync(...)
  -> SaveChangesAsync()
```

Post Process orchestration:

```text
PostProcessCommitCase(...)
  -> lifecycle/correction operation if staged
  -> memo operation if staged
  -> SaveChangesAsync() ONCE
```

Do this inside the Application layer.

These helpers may be `internal` classes/services/functions.

Do not expose a new public Domain interface merely to share implementation.

Do not duplicate authorization/event logic independently inside Post Process.

---

# 25. Per-Case failure cleanup

For a failed Case commit:

- discard EF tracked changes for that Case operation;
- discard pending event-sink state;
- continue to the next Case.

The existing `IUnitOfWork.DiscardChanges()` may be reused if the Post Process commit loop is structured so each Case is prepared/committed sequentially.

Be careful that failed Case state does not contaminate the next Case.

---

# 26. Commit request shape

A dedicated Post Process endpoint is preferred.

Conceptually:

```text
POST /api/post-process/commit
```

Request:

```text
items: [
  {
    caseId
    expectedCurrentVersion

    lifecycleChange?: {
      type: Hit | Away | Cancel | CorrectToHit | CorrectToAway
      correctionReason?
    }

    memoChange?: {
      expectedVersion
      value
    }
  }
]
```

Do not expose both SalesMemo and TraderMemo mutation choices to the client.

The server should resolve the current user's own-side memo according to existing role semantics.

If the existing API architecture makes an explicit memo kind unavoidable, keep it typed and authorization-protected; never let the client gain access to the opposite-side memo merely by changing a field.

Return per-Case results.

---

# 27. Optimistic concurrency

Keep RFQ lifecycle and memo concurrency independent.

A pending Case may therefore carry:

```text
expectedCurrentVersion
expectedMyMemoVersion
```

Do not flatten these into one fake shared version.

On commit:

- lifecycle uses RFQ Case version;
- memo uses memo version;
- any required version conflict fails that Case atomically.

Do not auto-merge conflicts in the browser.

---

# 28. Outcome Correction implementation changes

Update existing correction behavior, not only Post Process.

Required corrections:

1. reason becomes required;
2. current Business Date must equal `ClosedBusinessDate`;
3. event records:
   - actor;
   - timestamp;
   - Business Date;
   - quote;
   - from;
   - to;
   - reason.

Preserve Contact Owner authorization.

Existing Sales/Trader screens calling correction must be updated to provide a reason or their correction UX must be adjusted minimally so they remain valid.

Do not leave an alternate endpoint that bypasses the reason/Business Date rule.

---

# 29. Close and Cancel Business Date recording

When performing:

```text
Hit
Away
Cancel
```

resolve the current Business Date in the Application layer before the Domain/event mutation.

For Hit/Away:

```text
ClosedBusinessDate
```

must become part of the Closed lifecycle Domain state.

For Cancel:

the Business Date may remain an event/projection fact unless another Domain rule needs it.

Do not infer these dates later from `OccurredAt`.

---

# 30. Event persistence

Extend the event contract so relevant Post Process business events carry Business Date.

The exact event payload organization may follow the repository's existing persistence-contract style.

At minimum support:

```text
ClosedHit.BusinessDate
ClosedAway.BusinessDate
Cancelled.BusinessDate
OutcomeCorrected.BusinessDate
```

Expose Business Date in read event models only where useful.

Because the application is unreleased, do not add compatibility machinery solely for old development event payloads.

Still keep the persistence contract explicit and typed.

---

# 31. Post Process query implementation

Create an Application query interface dedicated to this screen, for example:

```text
IPostProcessQueries
```

Avoid overloading the current `IEodQueries`.

Suggested request dimensions:

```text
Preset:
  Today
  Unclosed

Scope:
  Mine
  AllPermitted
```

The backend receives/uses the current Business Date from `IBusinessDateProvider`.

Do not let the browser submit an arbitrary historical Business Date for editing.

No date picker in v1.

---

# 32. Today projection details

A practical query design is:

```text
non-Draft Case
AND
(
  CreatedBusinessDate == currentBusinessDate
  OR relevant terminal/correction event BusinessDate == currentBusinessDate
)
```

Relevant event types:

```text
ClosedHit
ClosedAway
Cancelled
OutcomeCorrected
```

This allows:

- an RFQ created yesterday but closed today to remain visible in Today;
- a Hit corrected today to remain visible;
- an old untouched RFQ to remain out of Today.

Memo-only activity does not pull a Case into Today.

Quote-only activity does not pull a Case into Today.

---

# 33. Last Changed By / At

Expose:

```text
LastChangedBy
LastChangedAt
```

for operational/audit context.

Use authoritative persisted actor/timestamp data.

Do not invent actor identity in React.

For v1, define “Last Changed” as the latest relevant Case/business mutation that can be authoritatively projected from existing event/mutation metadata.

If memo metadata is not currently persisted with actor/timestamp, do not build a large audit redesign merely for this column.

Either:

- add minimal memo update metadata cleanly; or
- document that v1 Last Changed tracks event-backed Case changes and not memo-only edits.

Prefer a truthful definition over fabricated completeness.

---

# 34. Action authorization in the Grid

Do not duplicate authorization rules as the security boundary in React.

UI should hide/disable clearly ineligible actions for usability, but backend authorization remains authoritative.

Existing rules continue, including:

```text
Hit/Away Close      -> Contact Owner
Outcome Correction  -> Contact Owner
Cancel              -> Contact Owner
Memo update         -> existing Sales/Trader role rule
```

Post Process visibility does not grant mutation permission.

---

# 35. Commit success reconciliation

After commit, reconcile only the affected Cases/results rather than blindly pretending every row succeeded.

## In `Unclosed`

A successfully Hit/Away/Cancelled Case no longer belongs to the preset.

Remove it from the visible Grid.

## In `Today`

A successfully closed/corrected Case still belongs to Today.

Patch/reload it to the authoritative latest state and keep it visible.

## Failed Case

Keep it visible.

Keep its pending change so the operator can inspect/fix/retry.

Do not discard failed pending changes automatically.

## Successful memo-only Case

Clear its pending diff and patch its memo/version authoritatively.

---

# 36. Result Bar

After commit, show a compact Result Bar rather than another result modal.

Example:

```text
3 succeeded / 1 skipped / 2 failed
```

Allow details to show:

```text
CaseId
status
code
message
```

Reuse the Trader/Sales Result Bar interaction style where practical.

Do not show raw JSON.

---

# 37. Grid selection

Use Excel-like AG Grid selection with no checkbox column, consistent with the redesigned Sales/Trader workstations.

But Post Process lifecycle buttons are row actions/staging actions, not bulk immediate commands.

Do not create a second selection-based bulk lifecycle workflow in v1.

The primary bulk behavior is:

```text
stage multiple rows independently
-> Confirm Changes
-> commit all pending Cases
```

---

# 38. Search / historical editing

Do not add arbitrary historical date selection.

This is deliberate.

Post Process v1 offers:

```text
Today
Unclosed
```

not:

```text
Yesterday
Last Week
Pick Date
History
```

Past closed business should not become casually editable through a date picker.

If investigation of older cases is needed, use the existing Search functionality elsewhere.

---

# 39. Statistics

Do not implement Statistics yet.

Do ensure the data written in this phase will make future statistics possible.

Preserve/record sufficient facts for future aggregation such as:

```text
Business Date
Sales
Trader
Contact Owner
Client
Category
Notional
final outcome
quote context
actor/timestamps
```

No Statistics tab is required now.

Do not retain the old EOD Summary card as a substitute for future Statistics.

---

# 40. Suggested frontend state model

Keep local pending state keyed by CaseId.

Conceptually:

```ts
type PendingPostProcessChange = {
  caseId: number
  baseCurrentVersion: number

  lifecycle?:
    | { type: 'Hit' }
    | { type: 'Away' }
    | { type: 'Cancel' }
    | { type: 'CorrectToHit'; reason: string }
    | { type: 'CorrectToAway'; reason: string }

  memo?: {
    baseVersion: number
    value: string
  }
}
```

This is illustrative, not a required exact type.

Important properties:

- one object per Case;
- selection-independent;
- survives preset/scope switches;
- removable via a Clear/Revert action;
- lifecycle choice replaces prior staged lifecycle choice;
- lifecycle and memo can coexist.

Do not store a cloned entire RFQ row as the pending source of truth if a minimal diff is sufficient.

---

# 41. Row Clear/Revert

Provide a small way to clear staged changes for a row.

It may be part of the Action cell.

`Clear` / `Revert` should restore the displayed staged fields to the currently loaded authoritative values and remove that Case from pending state.

Do not send anything to the backend.

---

# 42. UX failure behavior

Expected business/concurrency failures should remain recoverable.

Examples:

```text
VersionConflict
Forbidden
Validation
DomainRuleViolation
```

Show per-Case result details.

Do not turn one expected Case failure into a screen-wide fatal error.

Unexpected failures may use the existing incident/error middleware behavior.

---

# 43. Existing EOD query

The current `EfCoreEodQueries` uses:

```text
DeskDateBoundary.ToUtc(date)
DeskDateBoundary.ToUtc(date + 1)
```

and timestamp-range filtering.

That is a local calendar date query, not the Business Date semantics required here.

Do not reuse this logic for Post Process Today membership.

The old EOD query may be deleted or left unused temporarily, but Post Process must have a Business-Date-based query.

---

# 44. Migration/data model guidance

Add the minimum persistence required for the new business facts.

Likely changes include:

- Case/current persistence for `CreatedBusinessDate`;
- closed lifecycle/current persistence for `ClosedBusinessDate`;
- event payload persistence for relevant Business Dates;
- any read-model-supporting indexes required by `Today` / `Unclosed`.

Do not add a generic `LastActivityBusinessDate` column merely to shortcut the query unless a demonstrated performance need appears.

This dataset is currently small enough to prefer semantic clarity first.

---

# 45. Indexing

Add straightforward indexes only where obviously useful.

Examples may include:

```text
CreatedBusinessDate
current lifecycle/status
event Business Date/type
```

depending on the final schema.

Do not prematurely build an analytics projection infrastructure.

---

# 46. Tests — Domain

Add Domain tests for at least:

- Hit close retains `ClosedBusinessDate`;
- Away close retains `ClosedBusinessDate`;
- Hit -> Away correction retains the original `ClosedBusinessDate`;
- Away -> Hit correction retains the original `ClosedBusinessDate`;
- lifecycle invariants remain valid.

If Business Date equality is enforced in Application instead of the transition itself, test that at the Application level.

---

# 47. Tests — Application

Add tests for:

- close obtains current Business Date and persists it;
- cancel records current Business Date;
- correction succeeds on the same Business Date;
- correction fails on a different Business Date;
- correction reason is mandatory;
- Contact Owner authorization still applies;
- same-Case lifecycle + memo commit is atomic;
- memo conflict rolls back lifecycle for that Case;
- lifecycle conflict rolls back memo for that Case;
- failure of Case B does not roll back successful Case A;
- failed Case changes are discarded before processing the next Case;
- Mine predicate is the agreed OR of Sales / Contact Owner / Assigned Trader;
- visibility policy seam is invoked for All permitted.

---

# 48. Tests — Infrastructure/query

Add query/persistence tests for:

## Today

- created today -> included;
- old open Case closed today -> included;
- old Hit corrected today -> included;
- old untouched closed Case -> excluded;
- memo-only update today -> does not make old Case Today;
- quote-only update today -> does not make old Case Today;
- Draft -> excluded.

## Unclosed

- Active from prior Business Date -> included;
- Presented from prior Business Date -> included;
- Hit/Away/Cancelled -> excluded;
- Draft -> excluded.

## Scope

- Mine uses:
  - SalesId;
  - ContactOwnerId;
  - AssignedTraderId;
- All permitted goes through visibility policy.

## Persistence

- Business Date round-trips for close lifecycle/event facts.

---

# 49. Tests — frontend

Add focused tests for:

- Today / Unclosed switch;
- Mine / All permitted switch;
- pending changes survive those switches;
- Draft never rendered in Post Process results;
- lifecycle buttons stage rather than immediately mutate;
- changing Hit -> Away replaces staged outcome;
- memo and lifecycle can be staged together;
- correction enables required Correction Reason;
- Confirm Changes count is Case count;
- confirmation dialog lists only pending Cases;
- Refresh warns/discards pending changes only after confirmation;
- successful Unclosed close disappears;
- successful Today close remains with new state;
- failed commit keeps pending diff;
- left-edge pending marker appears;
- Hit/Away/Cancelled state styling is distinct from pending styling;
- no opposite-side memo is displayed;
- no quote cell is editable.

---

# 50. API/OpenAPI

If adding/changing API contracts:

- regenerate the OpenAPI artifact using the repository's normal process;
- regenerate TypeScript API types;
- do not hand-edit generated schema files.

Keep contracts strongly typed.

Avoid generic string action names when an enum/discriminated structure is practical.

---

# 51. Quality/build validation

Run the normal repository validation.

At minimum:

```bash
dotnet test
```

and:

```bash
cd src/Rfq.Web
npm test -- --run
npm run build
npm run quality
```

If the repository-level quality script is intended to cover both stacks, also run:

```bash
node scripts/quality.js
```

Report any environment-specific failure rather than silently skipping it.

---

# 52. Implementation boundaries

Prefer changes around:

```text
Domain
  closed lifecycle Business Date

Application
  Post Process query contract
  visibility seam
  commit orchestration
  commit-less reusable mutation operations
  Business Date rules

Infrastructure
  persistence/migration
  Post Process query
  event Business Date persistence
  visibility implementation

API
  Post Process query/commit endpoints

Web
  Post Process workspace/grid
  local pending-diff model
  confirm/result UX
```

Do not refactor unrelated Sales/Trader code except where:

- shared commit-less operations require it;
- correction reason/Business Date rules require callers to remain valid.

---

# 53. Final implementation report

When finished, report:

1. final Post Process route/screen structure;
2. Business Date persistence design;
3. exact `Today` and `Unclosed` query semantics;
4. visibility policy implementation;
5. Mine logic;
6. Post Process read-model fields;
7. staging model;
8. per-Case atomic commit implementation;
9. how existing Close/Correction/Memo use cases were refactored for commit-less reuse;
10. correction reason and same-Business-Date enforcement;
11. success/failure reconciliation behavior;
12. tests/build/quality commands executed and results;
13. commit SHA(s);
14. any deviation from this instruction and why.

Do not continue into Statistics, Booking, Ticket generation, or a historical Post Process redesign after completing this phase.
