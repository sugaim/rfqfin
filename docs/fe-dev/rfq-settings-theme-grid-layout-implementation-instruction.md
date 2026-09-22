# RFQ Web — Personal Settings, Theme, and Grid Layout Implementation Instruction

## 0. Baseline and intent

Implement the next RFQ Web follow-up on top of the current repository baseline:

- `ac27b5c22988dbf3906275b32cb70aee38e3f574` — Post Process v1
- a focused Post Process follow-up may land before this work; preserve it and do not undo unrelated fixes

The application is still unreleased. Do not build compatibility machinery for development-only persisted UI settings when a simple reset/default fallback is sufficient.

This change has three connected goals:

1. replace the current Trader-only Preferences modal with one coherent personal **Settings Drawer**;
2. make the whole RFQ Web application switch cleanly between **Light** and **Dark** themes, including Ant Design, AG Grid, and custom application CSS;
3. move Grid layout persistence out of permanent toolbar/footer controls and into each Grid's **right-click context menu** with `Save / Load / Reset`.

Do not turn this into a generic configuration platform or a broad visual redesign.

---

# 1. Product scope

Settings in this phase are **personal settings only**.

Do not add:

- Desk Settings;
- Category routing management;
- admin/user management;
- organization settings;
- arbitrary color customization;
- named Grid layout presets;
- System/OS-following theme mode;
- a generic JSON settings bag.

The settings surface is available only when the current identity has the **Trader** role.

The settings entry point must not become a top-level navigation destination.

---

# 2. Settings entry point and Drawer

Add a small, unobtrusive Settings control in the **upper-right application chrome**.

Conceptually:

```text
Sales | Trader | Post Process                     ...  Settings
```

Only show the Settings entry point when `/api/me` reports the `Trader` role.

Do not keep the current `Trader Preferences` button/modal as a second settings surface.

Clicking Settings opens an Ant Design **Drawer**.

The Drawer contains personal settings only and should be compact rather than dashboard-like.

Suggested grouping:

```text
Settings

Appearance
  Theme                [ Light | Dark ]

Trader Preferences
  Default Quote Mode   [ Calculated | Manual ]
  Default Quote Expiry [ None | 15 min | 1 hour ]

                                      [ Save ]
```

Use one explicit **Save** action.

Opening the Drawer initializes a local draft from the persisted values.

Changing controls inside the Drawer must not persist immediately.

Closing/cancelling the Drawer without Save discards unsaved Drawer edits.

After a successful Save:

- persist the changed personal settings;
- apply the selected theme to the whole running application;
- keep the saved values after reload/login;
- close the Drawer or otherwise clearly leave it in a saved state.

If Save fails, keep the Drawer open and preserve the user's draft values while showing an ordinary application error state.

---

# 3. Theme setting

Support exactly:

```text
Light
Dark
```

Do **not** add `System`, `Auto`, or `prefers-color-scheme` behavior.

The persisted default for an unset Theme is **Dark**, preserving the current application appearance.

Theme is a user/application preference, not RFQ business Domain data.

Do not put Theme concepts into `Rfq.Domain`.

---

# 4. Theme persistence API

Keep the existing explicit `/api/me/settings/...` pattern.

Do not replace the current setting APIs with a generic umbrella configuration endpoint merely for this work.

Existing endpoints remain:

```text
GET /api/me/settings/quote-expiry
PUT /api/me/settings/quote-expiry

GET /api/me/settings/default-quote-mode
PUT /api/me/settings/default-quote-mode
```

Add the corresponding Theme endpoint:

```text
GET /api/me/settings/theme
PUT /api/me/settings/theme
```

Use a typed contract representing only `Light | Dark`.

Implement persistence consistently with the existing personal settings implementation. The current personal quote settings live on `MasterUserEntity`; adding a small persisted Theme preference there is acceptable.

A missing/null persisted Theme must resolve to `Dark`.

Regenerate/update:

- OpenAPI artifact;
- generated frontend API schema;
- frontend RTK Query bindings/types.

Do not make the frontend depend on raw persistence strings outside the API boundary.

---

# 5. Existing Trader preference semantics

## 5.1 Default Quote Mode

The user-selectable values are:

```text
Calculated
Manual
```

The default when no setting is persisted remains:

```text
Calculated
```

The setting applies only when a **new Working Quote** is created for that Trader.

Changing this setting must **not** retroactively switch an already-existing Working Quote between Calculated and Manual.

Preserve the existing business/application behavior that resolves the Assigned Trader's preference when creating the Working Quote.

Do not move this rule into React.

---

## 5.2 Default Quote Expiry

The Settings Drawer exposes only these presets:

```text
None
15 min
1 hour
```

The underlying typed domain semantics remain:

```text
QuoteExpiry.None
QuoteExpiry.After(duration)
```

Do not weaken the Domain model back to nullable/loosely typed expiry merely because the UI exposes presets.

The Settings value is the Trader's **default for future Quote Confirmation**.

It initializes the expiry choice used when confirming a quote. The effective expiry is captured by the Confirmed Quote as it is today.

Changing the default must not mutate an already-confirmed quote or its already-resolved expiry.

Per-RFQ/per-confirmation override behavior remains available where it already exists.

The current development seed contains a `5 minute` default for one Trader. That is no longer one of the supported Settings presets. Update development seed/test data to a supported preset, preferably `15 min`, rather than adding `5 min` to the new Settings UI.

Do not restrict the core `QuoteExpiry.After` type itself to only 15/60 minutes; the preset restriction is a UI/personal-setting surface decision.

---

# 6. Central theme architecture

The repository already has:

```text
src/Rfq.Web/src/app/theme.ts
```

with a fixed Ant Design dark algorithm and fixed AG Grid dark mode.

Promote this into the single application theme source instead of scattering Light/Dark checks across components.

Conceptually provide one theme mode value:

```ts
type AppThemeMode = 'Light' | 'Dark'
```

and derive from it:

1. Ant Design `ThemeConfig`;
2. AG Grid theme mode;
3. root application theme attribute / CSS variables;
4. browser `color-scheme`.

For Ant Design:

```text
Dark  -> theme.darkAlgorithm
Light -> theme.defaultAlgorithm
```

`ConfigProvider` must react to the current application theme state.

For AG Grid, replace the fixed:

```text
data-ag-theme-mode="dark"
```

behavior with the matching Light/Dark value using the repository's current AG Grid theme mechanism.

For custom CSS, set one stable root attribute, for example:

```html
<html data-app-theme="dark" ...>
```

or an equivalent class/attribute, and resolve application semantic CSS variables from it.

Do not implement Light theme by applying a CSS filter or global inversion.

---

# 7. Remove fixed-dark blockers

The current application contains fixed-dark assumptions that must be removed or made theme-aware.

At minimum address:

```text
:root color-scheme: dark
body background: #000
.app-title color: #fff
.app-content background: #141414
AppShell <Menu theme="dark">
app/theme.ts -> theme.darkAlgorithm
applyGlobalTheme() -> data-ag-theme-mode = dark
```

The header/navigation must follow the selected application theme. Do not leave a permanently dark Ant Menu inside Light mode.

The resulting Light theme should be a real Light theme across the application, not a mostly-light page with isolated dark panels.

---

# 8. Color refactor principle

The repository currently uses many literal colors in `styles.css`.

Do not merely duplicate all current selectors under `.light` and `.dark` with another set of literals.

Introduce **semantic application color tokens**, preferably CSS custom properties, and let Light/Dark provide concrete values for those tokens.

Name tokens after **meaning**, not appearance.

Good:

```text
--surface-page
--surface-content
--surface-panel
--border-default
--text-secondary
--row-sales-quoted-bg
--row-trader-attention-high-bg
--row-selection-indicator
```

Avoid:

```text
--dark-gray-1
--orange-2
--purple-500
--light-blue
```

The same semantic token may resolve to different concrete colors in Light and Dark themes.

Dark theme should preserve the current visual appearance as closely as practical while performing this refactor.

---

# 9. Structural/custom CSS color inventory

The current custom CSS contains more hard-coded color than only the RFQ row states. Theme all of these categories.

## 9.1 Global surfaces and text

Current examples include:

```text
#000
#141414
#111
#0d0d0d
#171717
#15191c
rgba(255,255,255,...)
#fff
#8c8c8c
#777
#707070
```

These represent page/surface backgrounds, primary text, secondary text, tertiary text, and panel surfaces.

Move them behind structural semantic tokens such as:

```text
--app-page-bg
--app-content-bg
--app-panel-bg
--app-panel-raised-bg
--app-text
--app-text-secondary
--app-text-tertiary
```

Do not leave a panel dark merely because it was overlooked by Ant Design theming.

---

## 9.2 Borders and separators

Current repeated literals include:

```text
#303030
#292929
#262626
```

Use semantic border/separator tokens instead of literal dark grays.

Examples:

```text
--app-border
--app-border-subtle
--app-separator
```

This applies to:

- Sales grid shell;
- Sales work pane;
- Trader sections;
- Trader side pane;
- search filters;
- result bars/tables;
- memo/owner separators;
- Post Process confirmation table.

---

## 9.3 Neutral pane accent

The current Sales/Trader pane top/source accent uses values such as:

```text
#45667f
```

Treat this as a semantic neutral/information pane accent, not as a fixed blue literal.

It must remain visible with suitable contrast in both themes.

---

# 10. RFQ semantic row/state colors

These colors are business/UI semantics and must be kept distinct from generic structural colors.

Do not collapse them into `warning = orange` or into one generic row highlight without preserving their meaning.

## 10.1 Sales Grid

Current semantics:

```text
sales-row-quoted
  Active + Quoted
  amber attention background

sales-row-draft
  Draft
  purple-ish draft background

sales-row-terminal
  Cancelled / Hit / Away
  subdued foreground/background

sales-row-selected
  selected-row left-edge marker
```

Use semantic theme tokens such as:

```text
--row-sales-quoted-bg
--row-sales-draft-bg
--row-terminal-bg
--row-terminal-fg
--row-selection-indicator
```

The concrete color can differ between Light/Dark while preserving the same meaning and relative emphasis.

---

## 10.2 Trader Grid

Current semantics:

```text
trader-row-attention-high
  AssignedTrader == current user AND not owned
  strongest attention background

trader-row-attention-work
  AssignedTrader == current user AND owned AND Quote Requested
  working-attention background

trader-row-terminal
  Cancelled / Hit / Away
  subdued foreground/background

trader-row-selected
  selected-row left-edge marker
```

Use distinct semantic tokens:

```text
--row-trader-attention-high-bg
--row-trader-attention-work-bg
--row-terminal-bg
--row-terminal-fg
--row-selection-indicator
```

Do not encode these as `redRow`, `orangeRow`, etc.

---

## 10.3 Post Process Grid

Current semantics:

```text
post-process-row-unclosed
  Active / Presented
  light amber attention background

Hit
  normal row background
  State tag/text uses success semantic

Away
  normal row background
  State tag/text uses error semantic

post-process-row-cancelled
  subdued foreground/background

post-process-row-pending
  uncommitted local-change left-edge marker
```

Use semantic tokens such as:

```text
--row-post-process-unclosed-bg
--row-terminal-bg
--row-terminal-fg
--row-pending-change-indicator
```

The pending-change indicator and selection indicator may currently use the same concrete blue, but they are **not the same semantic token**.

Keep them separate:

```text
--row-selection-indicator
--row-pending-change-indicator
```

This prevents future visual changes to selection from accidentally changing the meaning of uncommitted Post Process edits.

---

# 11. Other semantic application colors that must not be missed

The custom CSS also contains state/meaning colors outside whole-row backgrounds.

Theme/refactor these as semantic tokens as appropriate:

## Amendment state

Current implementation includes:

- changed-cell background;
- changed-cell bottom marker;
- amendment-diff left marker/background;
- Sales pane draft/new accent.

Keep amendment/draft semantics distinct from generic selection.

Suggested semantic family:

```text
--amendment-changed-bg
--amendment-changed-indicator
--amendment-panel-bg
--amendment-panel-indicator
--draft-accent
```

## Sales pane lifecycle accent

Current pane top-border meaning includes:

```text
Quoted
Draft/New
Hit/Away/Cancelled
```

Keep these meaningful in both themes rather than leaving current dark-theme literals such as `#ad7b21`, `#6750a4`, `#5b5b5b` in selectors.

## Bulk Result Bar

Current result bars use separate left-border semantics for:

```text
Succeeded
Warning/Skipped
Error/Failed
```

Keep these semantic result colors and theme their concrete values.

## Calculation failure

`.calc-failed` is an error/failure badge and must remain an error semantic in both themes.

## Pricer source

`.pricer-source` uses a special panel background and left accent. Treat it as a source/context information surface and theme it rather than hard-coding dark-only values.

---

# 12. Ant Design semantic colors

Several components already use Ant Design semantic/preset color APIs, for example:

```text
Alert type="error" / "warning"
Badge status="warning" / "processing" / "default"
Tag success / error / warning / default
Button danger
health Tag processing / success / error
Business Date Tag blue
Sales bulk eligibility Tag green/default
Sales revision-kind Tag gold/blue
Post Process HIT/AWAY state Tags success/error
```

Do **not** replace these with raw hex values.

They are already meaning-based and should be allowed to follow the selected Ant Design theme.

Where an Ant preset color is being used as a true categorical distinction, keeping the named Ant preset is acceptable.

The main requirement is that no component remains visually tied to Dark mode because of custom literal CSS or a fixed `theme="dark"` prop.

---

# 13. Do not make colors user-editable

This Settings phase supports theme selection only.

Do not expose controls such as:

```text
Quoted Row Color
Attention Color
Hit Color
Away Color
Selection Color
```

The semantic palette is application design, not user configuration.

---

# 14. Grid layout settings are not part of the Settings Drawer

Grid layout persistence remains a per-Grid concern.

Do **not** put Grid layout Save/Load/Reset inside the Settings Drawer.

Do **not** consume permanent vertical toolbar space for Grid layout controls.

Use each AG Grid's **right-click context menu**.

Provide a compact submenu, conceptually:

```text
Grid Layout >
  Save
  Load
  Reset
```

No named presets are required.

---

# 15. Grid surfaces and stable persistence keys

Support the persisted Grid layout behavior for these current working Grids:

```text
Sales Main
  screenId = sales
  configKey = main

Trader Main
  screenId = trader
  configKey = main

Trader Search
  screenId = trader
  configKey = search

Trader Quote Confirmation
  screenId = trader
  configKey = confirm

Post Process Main
  screenId = post-process
  configKey = main
```

Reuse the existing:

```text
GET /api/me/grid-configs/{screenId}/{configKey}
PUT /api/me/grid-configs/{screenId}/{configKey}
```

persistence boundary.

Do not create a second Grid preference store.

---

# 16. Grid Save semantics

`Save` explicitly persists the current Grid **layout**.

Do not autosave layout on every drag/resize.

Persist layout state required for the user-visible arrangement, including where supported by the Grid:

```text
column visibility
column order
column width
pinned state
sort state / sort order
row-group / pivot column state if present
column-group open/closed state
```

Prefer a small explicit config payload, for example conceptually:

```json
{
  "columnState": [...],
  "columnGroupState": [...]
}
```

rather than serializing arbitrary Grid runtime state.

The backend config JSON is intentionally opaque; keep UI-layout knowledge in the frontend.

---

# 17. Grid state that must NOT be saved

Do not persist:

```text
filters
quick-filter/search text
Sales filter preset
Trader search criteria
row selection
focused cell
scroll position
currently open context menu
cell edit state
Draft/amendment input
Post Process pending changes
temporary validation/error state
Live/Paused mode
result bars
```

Grid layout persistence must not accidentally become workflow-state persistence.

---

# 18. Grid Load semantics

The current application already loads persisted Grid configuration when the relevant Grid is initialized. Preserve that useful behavior.

The new explicit `Load` context-menu action means:

```text
re-apply the last server-saved layout now
```

This allows a user to make temporary local column changes and then discard them by choosing Load.

`Load` must not fetch/apply filters, selection, scroll, pending edits, or other excluded state.

If no saved layout exists, disable `Load` or make it a harmless no-op; do not surface the ordinary absence of a saved config as a screen-level failure.

If saved layout JSON is invalid/incompatible, fall back safely to the application-defined default Grid layout.

Because the application is unreleased, do not build a complex migration framework for old development-only Grid JSON.

---

# 19. Grid Reset semantics

`Reset` restores the **code-defined system default layout** for that Grid.

It must:

- reset column state to the current application default;
- reset column-group open state to the current application default where relevant;
- leave business/workflow state untouched.

`Reset` does **not** automatically overwrite the user's persisted server layout.

Therefore:

```text
Reset
  -> current Grid becomes system default
  -> persisted saved layout remains unchanged

Reset + Save
  -> system default becomes the user's newly persisted layout
```

Do not add a backend DELETE merely to implement Reset in this phase.

---

# 20. Integrate context menus without losing existing actions

Sales already has a meaningful row context menu.

Do not replace its existing RFQ/business actions.

Append the Grid Layout submenu cleanly, separated from business actions.

For Trader and Post Process Grids, add the layout submenu without turning ordinary right-click into a large settings UI.

Quote Confirmation is also an independent persisted Trader Grid and should receive the same layout submenu.

Prefer a small shared frontend helper for:

- capturing layout state;
- applying layout state;
- resetting layout;
- producing the `Grid Layout` context-menu submenu;

rather than duplicating slightly different Save/Load/Reset logic across five Grid surfaces.

Do not build a generic UI framework beyond what these current Grids require.

---

# 21. Remove obsolete Grid/Preferences controls

After the context-menu and Settings Drawer are implemented, remove redundant permanent controls.

In particular, remove/replace current surfaces such as:

```text
Sales toolbar: Save Layout / Reset Layout
Trader pane footer: Preferences / Save Grid / Reset Grid
Trader Preferences modal
```

Do not leave two different ways to edit the same personal preference or Grid layout unless an existing workflow genuinely requires both.

The primary Grid layout interaction after this change is the right-click context menu.

---

# 22. Theme state ownership

Theme affects the entire application and therefore must not be owned only by `TraderScreen`.

Keep theme state at the application/AppShell level (or an equivalent application-level provider) so that:

- Sales;
- Trader;
- Post Process;
- Drawers;
- Modals;
- context menus / portal-rendered Ant components;
- AG Grids

all see the same current theme.

The Settings Drawer can be opened from AppShell and update the application-level theme state after successful Save.

Do not implement independent per-screen theme state.

---

# 23. Theme loading behavior

On application startup:

1. recognize the current user as today;
2. load the persisted Theme setting;
3. resolve missing Theme to Dark;
4. apply the resolved Theme to Ant Design, custom CSS, and AG Grid.

While the setting is loading, using the current Dark baseline temporarily is acceptable.

Do not add localStorage as a second authoritative Theme store merely to avoid a short startup transition. The DB-backed personal setting remains authoritative.

Changing development identity must naturally load/apply the newly selected user's Theme after the application reloads as it does today.

---

# 24. Theme completeness acceptance criteria

Both Light and Dark must be visually usable across at least:

```text
AppShell/header/navigation
Sales toolbar/grid/work pane/drawer/modal/result bar
Trader toolbar/main grid/search grid/side pane/pricer/confirmation modal/result bar
Post Process toolbar/grid/confirmation modal/result bar
Ant alerts/tags/badges/buttons/forms/selects
AG Grid headers/rows/context menus/editors/tooltips
```

Check specifically that:

- text remains readable;
- borders remain visible but subdued;
- attention rows remain distinguishable;
- selected-row marker remains visible;
- Post Process pending marker remains visible and distinct in meaning;
- terminal rows remain subdued without becoming unreadable;
- amendment changed cells remain identifiable;
- HIT success and AWAY error semantics remain recognizable;
- focus/hover/selected states do not disappear in Light theme.

Do not judge completion only from the Settings Drawer itself.

---

# 25. Backend tests

Add/adjust tests for Theme persistence:

- missing persisted Theme resolves to Dark;
- PUT Light persists and GET returns Light;
- PUT Dark persists and GET returns Dark;
- invalid Theme input is rejected by the normal API validation path;
- one user's Theme does not alter another user's preference.

Preserve existing tests for:

- Quote Expiry typed semantics;
- Default Quote Mode;
- Grid Config user scoping.

Update development seed/API tests that assumed a 5-minute expiry preset if necessary.

---

# 26. Frontend Settings tests

Add focused tests covering at least:

1. Settings entry point is visible for a Trader identity and absent for a Sales-only identity.
2. Settings opens a Drawer, not a top-level route or the old Trader Preferences modal.
3. Drawer initializes from persisted Theme / Default Quote Mode / Quote Expiry.
4. Changing Drawer values does not call save mutations until Save.
5. Closing without Save discards the local draft.
6. Save persists the selected values.
7. successful Theme Save switches Ant/custom/AG Grid theme state across the application.
8. saved Theme is restored when the application is mounted again.
9. Default Quote Mode remains Calculated when no explicit preference exists.
10. Settings only offers Quote Expiry presets `None / 15 min / 1 hour`.

Do not rely only on snapshot tests for theme behavior.

---

# 27. Frontend theme tests

Refactor the existing `app/theme.test.ts` so it verifies both modes rather than only fixed Dark mode.

At minimum verify:

```text
Dark -> Ant darkAlgorithm + AG Grid dark mode + root dark theme attribute
Light -> Ant defaultAlgorithm + AG Grid light mode + root light theme attribute
```

Also cover the AppShell navigation Menu so it is not permanently `theme="dark"` in Light mode.

Do not add tests for exact RGB values unless a particular semantic color is intentionally contractual.

Test semantic state/class assignment separately from concrete palette values.

---

# 28. Grid layout tests

Add focused tests for the shared Grid layout behavior:

- context menu contains `Grid Layout > Save / Load / Reset`;
- Save persists layout state only;
- Save includes column-group state where used;
- Load reapplies persisted layout after local column changes;
- Reset restores code-defined defaults;
- Reset does not call save by itself;
- filter/selection/scroll/pending edit are not part of the persisted payload;
- Sales business context-menu actions still exist;
- Trader Main/Search/Confirm use independent keys;
- Post Process Main uses its own key;
- no saved layout is handled without a screen-level error.

---

# 29. Quality and generated artifacts

Run the repository's normal formatting, lint, build, generated-schema, and test workflow.

At minimum ensure the relevant equivalents of:

```text
dotnet test
npm test
npm run lint
npm run format:check
npm run build
```

pass for the changed projects.

Regenerate the OpenAPI/frontend schema through the repository's existing generation process rather than manually editing generated contracts into an inconsistent state.

---

# 30. Commit discipline

Keep this as a focused Settings/Theme/Grid-layout change.

Do not mix in unrelated RFQ workflow redesign, Post Process business changes, database architecture changes, or calculation work.

Suggested commit boundary:

```text
add personal settings theme and grid layout context menus
```

---

# 31. Final acceptance summary

This change is complete when all of the following are true:

```text
Trader sees one unobtrusive top-right Settings entry.
Sales-only identity does not see it.
Settings opens a Drawer with explicit Save.
Theme choices are exactly Light/Dark and persist in DB.
Theme affects the full application: Ant + AG Grid + custom CSS.
Current hard-coded dark custom colors are routed through semantic theme tokens.
Sales/Trader/Post Process state coloring keeps its existing business meaning.
Selection and Post Process pending markers remain separate semantic tokens.
Ant semantic success/error/warning/processing/danger colors remain semantic, not raw hex replacements.
Default Quote Mode and Quote Expiry live in the same Drawer.
Default Quote Mode affects only newly created Working Quotes.
Quote Expiry settings offer None/15min/1h and act as the future confirmation default without mutating existing Confirmed Quotes.
Grid layout is not in the Settings Drawer.
Sales Main, Trader Main/Search/Confirm, and Post Process Main expose Grid Layout Save/Load/Reset through right-click context menus.
Grid Save persists layout, not workflow/filter/selection state.
Load reapplies the last saved layout.
Reset returns to code defaults without overwriting persisted layout until Save is explicitly chosen.
Old permanent Save/Reset/Preferences controls are removed where made redundant.
```
