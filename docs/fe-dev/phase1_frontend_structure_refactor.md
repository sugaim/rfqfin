# Phase 1 — Frontend Structure Refactor

## Goal

Refactor the current frontend structure before making any substantial UX changes.

The current baseline is:

- repository: `sugaim/rfqfin`
- baseline commit: `41d1036141491a601338617c0d692ccae4a6c5d6`
- frontend: `src/Rfq.Web`
- `src/Rfq.Web/src/App.tsx` currently contains App-level orchestration, shell/navigation, Sales UI, Trader UI, EOD UI, and most RTK Query wiring in one large file.

This phase is **structural refactoring only**, with two explicit exceptions:

1. introduce the agreed top-level URL routing skeleton;
2. apply a global dark theme.

Do not redesign Sales, Trader, or Daily Review UX yet.

---

## 1. Target architecture

Move toward this structure:

```text
src/
├─ app/
│  ├─ App.tsx
│  ├─ AppShell.tsx
│  ├─ router.tsx
│  └─ store.ts
│
├─ features/
│  ├─ sales/
│  │  ├─ SalesWorkspace.tsx
│  │  └─ SalesScreen.tsx
│  │
│  ├─ trader/
│  │  ├─ TraderWorkspace.tsx
│  │  └─ TraderScreen.tsx
│  │
│  └─ daily-review/
│     └─ DailyReviewWorkspace.tsx
│
├─ generated/
│  └─ api-schema.ts
│
├─ services/
│  └─ api.ts
│
├─ main.tsx
└─ styles.css
```

This is a guideline, not a requirement to create empty files or directories.

Do not create placeholder abstractions solely to match the tree.

In particular:

- do not create `settings/` yet if it has no implementation in this phase;
- do not create generic `hooks/`, `models/`, `utils/`, `components/` directories without an actual need;
- do not introduce an Atomic Design-style `atoms/molecules/organisms` hierarchy.

Prefer feature ownership over speculative reuse.

---

## 2. Add URL routing

Replace the current `activeView` local-state navigation with URL routing.

Use React Router in a lightweight SPA/declarative configuration.

Do not introduce loaders/actions, framework-mode routing, or another data-fetching architecture. RTK Query remains the application data layer.

Top-level routes:

```text
/sales
/trader
/daily-review
```

`/` should redirect to the same effective default currently used:

- development identity beginning with `trader-` → `/trader`
- otherwise → `/sales`

Unknown routes may redirect to the same default.

Do not introduce detailed routing yet, such as:

```text
/rfqs/:caseId
/trader/search
/trader?caseId=...
```

Those decisions belong to later UX phases.

Do not introduce role-based route guards in this phase unless required to preserve current behavior.

---

## 3. App responsibilities

After this refactor, `App` should no longer orchestrate all Sales and Trader mutations.

Keep App-level responsibilities limited to application-wide concerns such as:

- current application shell;
- routing;
- current development identity handling;
- current user / health / business-date information needed by the shell;
- global event/SSE handling;
- global “updates available” behavior.

Do not introduce a large `AppContext`.

Do not copy RTK Query data into a second global Redux slice merely to make it globally accessible.

RTK Query cache remains the source for server state.

---

## 4. AppShell responsibilities

Move `AppShell` out of `App.tsx`.

`AppShell` owns presentation of:

- RFQ header;
- top-level navigation;
- development identity selector;
- Business Date;
- Updates Available;
- API health.

Navigation should now reflect the current URL instead of `activeView` state.

Use these top-level labels:

```text
Sales
Trader
Daily Review
```

The existing EOD implementation may remain functionally unchanged underneath `Daily Review` for this phase.

Do not redesign the header.

Do not add Settings UI yet.

---

## 5. Global dark theme

Apply a dark theme globally in Phase 1.

This is an intentional visual change and is the only broad visual change allowed in this phase.

Requirements:

- use the framework-native Ant Design dark-theme mechanism rather than manually restyling each component;
- make AG Grid visually consistent with the dark application theme;
- ensure page/background/card/grid surfaces and text remain readable;
- keep existing layout, spacing, controls, and interaction model otherwise unchanged;
- do not introduce a custom brand palette or a theme-switcher yet;
- do not add light/dark user preference persistence in this phase.

The application should simply start in dark mode.

Avoid large CSS rewrites. Prefer theme configuration and only the minimum supporting CSS required.

---

## 6. Sales feature

Move the existing Sales UI into:

```text
features/sales/
```

Prefer separating:

```text
SalesWorkspace
SalesScreen
```

where:

### `SalesWorkspace`

Owns Sales-specific RTK Query wiring and orchestration currently located in `App`, including as appropriate:

- active Sales RFQ query;
- client/security candidate search;
- creation context;
- draft create/update/confirm/discard;
- Present / Unpresent;
- Hit / Away;
- amendment operations;
- create-from-existing;
- Cancel / Reopen;
- Sales Memo;
- Contact Owner operations;
- Sales grid config;
- Sales-specific loading/mutation aggregation.

### `SalesScreen`

Remains primarily the current UI/stateful presentation component.

Preserve current behavior.

Do **not** redesign:

- New RFQ entry;
- action placement;
- Grid layout;
- Draft UX;
- amendment UX;
- Memo UX;
- Booking workflow.

Booking handoff is a later phase.

---

## 7. Trader feature

Move the existing Trader UI into:

```text
features/trader/
```

Prefer:

```text
TraderWorkspace
TraderScreen
```

### `TraderWorkspace`

Owns Trader-specific RTK Query wiring and orchestration currently located in `App`, including as appropriate:

- active Trader RFQ query;
- ownership operations;
- working quote calculation/editing;
- Quote Confirm / Withdraw;
- trader lookup/candidate data;
- Trader Memo;
- Hit / Away and relevant lifecycle operations;
- scratch pricer call;
- Quote Expiry setting required by the current Trader UI;
- Trader-specific mutation aggregation.

### `TraderScreen`

Preserve the current UI behavior.

The current Pricer Drawer should remain a Drawer in Phase 1.

Do **not** yet implement:

- persistent/resizable Pricer pane;
- integrated historical Search;
- context menus;
- action hierarchy cleanup;
- reduced button density;
- new bulk UX.

Those are later Trader UX work.

---

## 8. Daily Review feature

Move the current EOD screen implementation into:

```text
features/daily-review/DailyReviewWorkspace.tsx
```

Route it at:

```text
/daily-review
```

Rename the top-level navigation/title from `EOD` to `Daily Review`.

However, preserve the current underlying functionality for now:

- EOD Summary;
- current search result grid;
- Changes/events.

Do not yet implement the agreed future Daily Review UX:

```text
Needs Action / All Today
same-day RFQs only
Memo completion
terminal-action workflow
multi-select bulk work pane
```

That will be a dedicated later phase.

---

## 9. API layer — important constraint

Do **not** split `services/api.ts` into hand-written per-feature API files in this phase.

The current hand-written RTK Query API is temporary. The intended direction is to eventually generate the API client/endpoints from OpenAPI.

Therefore:

- keep one current `createApi()` instance;
- keep existing endpoint definitions working;
- feature Workspaces may import the hooks they need from the existing API module;
- do not build a long-term architecture around `features/sales/api.ts`, `features/trader/api.ts`, etc.;
- do not duplicate endpoints;
- do not modify `generated/api-schema.ts` manually.

The long-term boundary is:

```text
generated API client
        ↓
frontend features
```

The physical structure of generated API code does not need to mirror frontend feature boundaries.

---

## 10. Shared components

Do not aggressively extract shared components during this phase.

In particular, keep separate feature-specific grids:

```text
SalesRfqGrid / SalesScreen
TraderRfqGrid / TraderScreen
DailyReview
```

Do not create a generic RFQ Grid just because these screens all display RFQs.

Only extract something into `shared/` when it is clearly:

1. business-feature-independent; and
2. genuinely reused.

Avoid speculative abstractions.

If no clear shared extraction is required by this refactor, do not create `shared/` yet.

---

## 11. Local development startup commands

Improve the local developer startup workflow as part of this phase.

The repository currently requires:

```bash
docker compose up -d
dotnet run --project src/Rfq.Api
```

for the backend, and:

```bash
cd src/Rfq.Web
npm run dev
```

for the frontend.

### Backend

Add a single repository-level convenience command/script that:

1. starts the PostgreSQL dependency with `docker compose up -d`;
2. starts `src/Rfq.Api`;
3. keeps the API in the foreground so Ctrl+C stops the API normally;
4. propagates meaningful failures from Docker or `dotnet run`.

Prefer a small cross-platform script using tooling already required by the repository (Node.js is acceptable).

Do **not** automatically execute:

```bash
dotnet run --project src/Rfq.DbTool -- reset-dev
```

because `reset-dev` is destructive and should remain an explicit developer action.

A target UX such as the following is sufficient:

```bash
node scripts/dev-backend.js
```

The exact file name may differ if there is a cleaner existing convention.

Update the README with the final command.

Do not add a new third-party process-manager dependency merely for this.

### Frontend

The existing frontend command is already acceptable:

```bash
cd src/Rfq.Web
npm run dev
```

Keep `npm run dev` as the canonical frontend development command.

If a repository-root convenience wrapper can be added trivially without introducing a new package-management layer, that is acceptable but not required.

Document the final frontend command in the README.

### Database initialization remains separate

Document that first-time/reset setup remains explicit:

```bash
docker compose up -d
dotnet run --project src/Rfq.DbTool -- reset-dev
```

Startup and database reset are intentionally separate concepts.

---

## 12. Preserve behavior

This is the primary acceptance criterion.

Apart from URL routing, the `EOD` → `Daily Review` label change, and the global dark theme, the structural refactor should not intentionally alter existing business behavior.

Preserve at least:

### Sales

- New / Save Draft / Confirm;
- saved Draft editing;
- amendments;
- Present / Unpresent;
- Hit / Away;
- Cancel / Reopen;
- Contact Owner change;
- Sales Memo including post-close editing;
- create-from-existing;
- bulk operations currently implemented;
- grid config behavior.

### Trader

- active RFQ display;
- Pick Up / Release / Assign / Take Over;
- calculated/manual quote workflow;
- Quote Confirm;
- Quote Expiry;
- Withdraw;
- Hit / Away;
- Contact Owner change;
- Trader Memo including post-close editing;
- current scratch Pricer.

### Application

- development identity switching;
- Business Date;
- API health;
- SSE/event update indication and refresh behavior.

---

## 13. Tests

Move/update frontend tests to match the new module structure rather than keeping compatibility exports in `App.tsx` solely for tests.

For example, tests should import:

```text
AppShell
SalesScreen
TraderScreen
```

from their actual new modules.

Preserve the existing behavioral test coverage.

Add focused routing tests sufficient to verify:

- `/sales` renders Sales workspace;
- `/trader` renders Trader workspace;
- `/daily-review` renders Daily Review;
- navigation changes URL;
- `/` resolves to the expected development-identity default.

Add or update a focused test sufficient to ensure the dark theme is applied at the application root where practical. Do not build brittle snapshot tests for colors.

Do not rewrite the entire test suite.

---

## 14. Do not do in Phase 1

Explicitly out of scope:

- Sales UX redesign;
- Booking integration;
- Trader Search integration;
- persistent/resizable Pricer;
- context-menu actions;
- button/action hierarchy redesign;
- Daily Review redesign;
- Needs Action rules;
- new bulk UX;
- Settings UI;
- RFQ Detail redesign;
- Revision/Quote History UI;
- role-based navigation redesign;
- OpenAPI endpoint/hook code generation;
- backend business/domain/API changes;
- API contract changes;
- broad shared-component extraction;
- light/dark theme switching;
- visual redesign beyond applying the global dark theme.

If a refactor appears to require one of these, prefer preserving the current implementation and leave it for the appropriate later phase.

---

## 15. Validation

From `src/Rfq.Web`, run at minimum:

```bash
npm test -- --run
npm run build
```

Also smoke-test the documented startup commands:

```bash
# backend convenience command generated in this phase
<backend command>

# frontend
cd src/Rfq.Web
npm run dev
```

Confirm that:

- API starts at `http://localhost:5100`;
- frontend starts at `http://localhost:5173`;
- Vite `/api` proxy continues to reach the backend;
- `/sales`, `/trader`, and `/daily-review` are directly loadable in development;
- browser refresh on each route works with the Vite dev server.

Fix regressions caused by the refactor.

No backend business/API behavior should need to change.

---

## 16. README

Update the relevant local-development section of `README.md` so that a developer can see, without reading implementation code:

- prerequisite setup;
- explicit database reset/seed command;
- one-command backend startup;
- frontend startup;
- URLs for API and frontend.

Keep it concise.

---

## 17. Commit

Make this work a single structural commit after tests/build pass.

Suggested commit message:

```text
refactor(web): split app into routed feature workspaces
```

The final response should contain:

1. short summary of the resulting structure;
2. important design decisions made during the refactor;
3. final backend startup command;
4. final frontend startup command;
5. tests/build executed and results;
6. commit SHA;
7. any deviations from this instruction and why.

Do not continue into Phase 2.
