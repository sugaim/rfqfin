<!-- BEGIN README.md -->

# RFQ Implementation Instructions

These instructions implement the canonical RFQ design incrementally.

## Authority

The canonical design specification is authoritative for business behavior.

Recommended companion package:

```text
rfq-design-spec/
  README.md
  01-domain-model.md
  ...
  10-design-decisions.md
```

If these implementation instructions and the canonical design conflict, **stop and follow the canonical design** unless the design has been explicitly revised.

These instructions deliberately specify implementation sequence, local-development setup, and completion checks. They are not a replacement for the canonical design.

---

## How to use these instructions with Codex / another coding agent

Give the agent:

1. the canonical design package
2. `00-technical-baseline.md`
3. `00-agent-rules.md`
4. exactly **one step file at a time**

Do not give several implementation steps and ask the agent to "continue until done".

Each step is designed to end in a runnable/testable repository state.

After each step:

1. inspect the diff
2. run the listed verification commands
3. manually exercise the listed scenario
4. commit
5. only then start the next step

If rate limits or an interrupted session occur, resume from the first uncommitted step. Do not ask the next agent to reconstruct unfinished intent from chat history.

---

## Sequence

### Foundation

- `01-solution-bootstrap.md`
- `02-database-foundation.md`

### First usable vertical slice

- `03-rfq-create-and-list.md`
- `04-master-search-and-defaults.md`
- `05-sales-draft-and-confirm.md`

### Trader workflow

- `06-trader-routing-and-ownership.md`
- `07-working-quote-and-calculation.md`
- `08-confirm-and-present-quote.md`
- `09-hit-away-and-contact-owner.md`

### Lifecycle completion

- `10-amendment-and-requote.md`
- `11-cancel-reopen-withdraw-expiry.md`

### Notifications / history / secondary screens

- `12-events-refresh-and-sse.md`
- `13-past-search-eod-pricer-grid-config.md`

### Hardening

- `14-concurrency-errors-and-integration-tests.md`
- `15-seed-data-and-demo-readiness.md`

---

## Expected repository shape

```text
Rfq.sln

src/
  Rfq.Domain/
  Rfq.Application/
  Rfq.Infrastructure/
  Rfq.Api/
  Rfq.DbTool/
  Rfq.Web/

tests/
  Rfq.Domain.Tests/
  Rfq.Application.Tests/
  Rfq.Infrastructure.Tests/
  Rfq.Api.Tests/

docker-compose.yml
global.json
Directory.Build.props
Directory.Packages.props
```

No `Shared`, `Common`, or `Contracts` project should be created initially.

---

## Dependency direction

```text
Rfq.Domain
    ↑
Rfq.Application
    ↑
Rfq.Infrastructure

Rfq.Api -> Application + Infrastructure
Rfq.DbTool -> Infrastructure
```

More precisely:

```text
Application    -> Domain
Infrastructure -> Application + Domain
Api            -> Application + Infrastructure
DbTool         -> Infrastructure
```

`Domain` must not depend on EF Core, ASP.NET Core, PostgreSQL, JSON serialization, or frontend concepts.

---

## Local development target

The normal local loop should eventually be:

```bash
docker compose up -d
dotnet run --project src/Rfq.DbTool -- reset-dev
dotnet run --project src/Rfq.Api
cd src/Rfq.Web
npm install
npm run dev
```

Visual Studio 2022 should be able to build/debug the .NET solution.

The API must never automatically reset or seed the database on normal startup.

<!-- END README.md -->

---

<!-- BEGIN 00-agent-rules.md -->

# Agent Rules for Every Implementation Step

Apply these rules to every step.

## 1. Scope discipline

Implement only the requested step.

Do not proactively build later features, generic frameworks, speculative abstractions, or "future-proof" subsystems.

Do not rewrite unrelated code merely to improve style.

If a later requirement is visible in the canonical design, preserve a reasonable extension point but do not implement the later feature.

A step may defer later behavior, but it must not knowingly violate a canonical invariant that is already applicable to the state it creates. Prefer a minimal real representation over a temporary no-op that leaves canonically invalid persisted state.

---

## 2. Preserve canonical terminology

Use the canonical domain terms exactly where practical:

- RFQ Case
- Revision
- WorkingQuote
- ConfirmedQuote
- Contact Owner
- Assigned Trader
- Owned
- RfqStatus
- QuoteStatus
- QuoteRequestReason
- Presented
- Hit
- Away

Do not invent synonyms such as:

- ActiveQuote as a business term
- RequoteStatus
- Amending status
- generic Closed status replacing Hit/Away

Internal implementation fields such as `CurrentQuoteId` are allowed where the design explicitly permits them.

---

## 3. No accidental architecture expansion

Do not introduce:

- MediatR unless explicitly requested later
- event sourcing framework
- message bus
- Redis
- distributed locks
- separate worker service
- microservices
- repository-per-table
- generic base repository
- generic Result framework for the whole solution
- AutoMapper solely to remove a few assignments
- custom logging abstraction over `ILogger<T>`
- `Shared` / `Common` dumping-ground projects

Simple explicit code is preferred.

---

## 4. Transactions

One business use case that changes multiple persistence objects must commit atomically.

Repository methods do not independently call `SaveChanges`.

Use the scoped EF Core `DbContext` behind Infrastructure repositories and an application-facing `IUnitOfWork`.

Do not expose `DbContext` to Domain or Application.

---

## 5. Date/time rules

Use:

```text
business dates -> DateOnly
instants        -> UTC timestamp
```

Do not use local server time as stored business truth.

Prefer an injectable time abstraction for use cases that depend on "now". Do not add a third-party time library initially unless a concrete need appears.

---

## 6. Numeric rules

Use `decimal` for persisted/application business values such as:

- notional
- price
- displayed yields/spreads where appropriate

The mock/internal calculation implementation may use `double` internally.

Do not attempt to implement production-quality financial numerical conventions in this project.

---

## 7. API style

Use ASP.NET Core Controllers.

API is code-first; OpenAPI is generated from the implementation.

Do not create a separate Contracts project initially.

API request/response DTOs may live in `Rfq.Api`.

---

## 8. Frontend style

Primary technologies:

- React
- TypeScript
- Vite
- Ant Design
- AG Grid Community
- Redux Toolkit / RTK Query

Use Ant Design for surrounding application UI:

- forms
- drawer
- modal
- tabs
- buttons
- date inputs
- select/autocomplete wrappers
- notifications/toasts

Use AG Grid for RFQ tabular workflows.

Do not rebuild a second grid using Ant Design Table for the main RFQ screens.

---

## 9. Main-grid refresh rule

Do not silently replace rows in an actively editable RFQ grid when server notifications arrive.

Later event/SSE work must indicate pending changes and reload only on explicit Refresh.

---

## 10. Testing rule

Every step must add tests appropriate to the code introduced.

Use:

- xUnit for .NET tests
- Testcontainers + real PostgreSQL for PostgreSQL-specific infrastructure tests
- Vitest for frontend unit/component logic where useful

Do not use EF Core InMemory as a substitute for PostgreSQL integration tests.

---

## 11. Completion response from coding agent

At the end of each step, the coding agent should report only:

1. what changed
2. important design decisions made within the allowed scope
3. commands run and results
4. exact manual verification procedure
5. any known limitation that belongs to a later step

Do not proceed into the next step automatically.

<!-- END 00-agent-rules.md -->

---

<!-- BEGIN 00-technical-baseline.md -->

# Technical Baseline

This file fixes the implementation baseline for the first working version.

## Backend

```text
IDE compatibility: Visual Studio 2022
.NET:              .NET 9
Target framework:  net9.0
C#:                C# 13
ASP.NET Core:      9
EF Core:           9
Npgsql EF provider:9.x
API style:         Controllers
Nullable:          enabled
```

Use current compatible patch releases within these major versions.

Do not target .NET 10 or .NET 11.

### Date/time baseline

Use `DateOnly` for business dates and UTC timestamps for instants.

Business `today` is not `DateTime.UtcNow.Date`. Resolve business dates through an injectable time abstraction using the configured desk/business timezone (initially JST / `Asia/Tokyo` for this JPY desk), then represent the result as `DateOnly`. Do not store local server time as business truth.

### Domain type style

For coarse typed lifecycle state, use ordinary C# constructs such as:

```csharp
abstract record RfqLifecycle;
sealed record DraftRfq(...) : RfqLifecycle;
sealed record OpenRfq(...) : RfqLifecycle;
sealed record CancelledRfq(...) : RfqLifecycle;
sealed record ClosedRfq(...) : RfqLifecycle;
```

Do not depend on preview C# union syntax.

---

## Database

```text
PostgreSQL
EF Core migrations
```

Migrations live with Infrastructure.

The API must not automatically run migrations or seed data.

A separate `Rfq.DbTool` project provides local/deployment-support commands:

```text
migrate
seed
reset-dev
```

`reset-dev` is development-only and may recreate the local database before migrating and seeding.

Production deployment mechanics are deliberately undecided.

---

## Frontend

```text
Node:              Node 24 LTS
React:             React 19.3
TypeScript:        5.8+
Vite:              current compatible Vite 8 release
Ant Design:        6.x
AG Grid:           Community 36.x
Redux Toolkit:     2.x
RTK Query:         included with Redux Toolkit
Package manager:   npm
```

Use current compatible patch releases, then commit `package-lock.json`.

Do not depend on experimental React features.

AG Grid Community is the baseline; do not use Enterprise-only APIs.

---

## Version locking

Backend:

```text
global.json
Directory.Packages.props
Directory.Build.props
```

Frontend:

```text
package.json
package-lock.json
```

Use central package management for .NET packages.

`Directory.Build.props` should at least enable:

```xml
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
```

Warnings-as-errors may be enabled for the solution's own code once bootstrap noise is under control. Do not block initial bootstrap on analyzer churn.

---

## Development runtime

Local PostgreSQL runs via Docker Compose.

Normal development components:

```text
PostgreSQL   -> Docker Compose
ASP.NET API  -> Visual Studio 2022 or dotnet run
React/Vite   -> npm run dev
```

Do not require Docker for the API or frontend during normal development.

---

## OpenAPI

The ASP.NET Core server implementation is authoritative.

Generate OpenAPI from the server.

Do not begin with a manually maintained contract-first specification.

Frontend API client generation may be added later if it proves useful; initial vertical slices may use explicit typed RTK Query endpoints.

<!-- END 00-technical-baseline.md -->

---

<!-- BEGIN 01-solution-bootstrap.md -->

# Step 01 — Solution Bootstrap

## Goal

Create a repository that builds in Visual Studio 2022, starts PostgreSQL locally, starts an ASP.NET Core 9 API, and starts a React/Vite UI.

No RFQ business behavior yet.

## Read first

- canonical `09-scope-and-deferred.md`
- `00-technical-baseline.md`
- `00-agent-rules.md`

## Implement

Create:

```text
Rfq.sln

src/
  Rfq.Domain
  Rfq.Application
  Rfq.Infrastructure
  Rfq.Api
  Rfq.DbTool
  Rfq.Web

tests/
  Rfq.Domain.Tests
  Rfq.Application.Tests
  Rfq.Infrastructure.Tests
  Rfq.Api.Tests
```

Configure project references according to the dependency direction in README.

Add:

- `global.json` targeting .NET 9 SDK
- `Directory.Build.props`
- `Directory.Packages.props`
- `.editorconfig`
- `.gitignore`
- `docker-compose.yml` with PostgreSQL
- basic README with local bootstrap commands

Backend:

- Controller-based ASP.NET Core API
- OpenAPI enabled
- `/api/health` endpoint returning simple health response
- Infrastructure DI extension method
- Application DI extension method
- DbTool command shell supporting command dispatch, but commands may still be placeholders except `--help`

Frontend:

- Vite + React + TypeScript
- Ant Design configured
- AG Grid Community registered correctly
- Redux Toolkit store + RTK Query base API
- one shell page with API health indicator
- basic top-level navigation placeholders for Sales / Trader / EOD, with no business behavior

## Do not implement

- domain RFQ entities
- EF schema
- migrations
- authentication
- security/client masters
- real navigation/router complexity unless needed for shell
- RFQ grid

## Completion criteria

All projects build.

PostgreSQL starts from Docker Compose.

API starts and `/api/health` responds.

Frontend starts and displays successful API health state.

Visual Studio 2022 can open/build the solution.

## Verify

```bash
docker compose up -d
dotnet restore
dotnet build

dotnet run --project src/Rfq.Api
```

Then separately:

```bash
cd src/Rfq.Web
npm install
npm run build
npm run dev
```

Open the frontend and verify the health indicator.

## Tests

Add a minimal API integration test for `/api/health`.

Add a minimal frontend test only if test runner bootstrap is introduced here; otherwise bootstrap Vitest in this step and test one trivial shell component.

## Commit boundary

Suggested commit:

```text
bootstrap solution and local runtime
```

<!-- END 01-solution-bootstrap.md -->

---

<!-- BEGIN 02-database-foundation.md -->

# Step 02 — Database Foundation

## Goal

Add PostgreSQL persistence infrastructure and a working migration/seed/reset tool without yet implementing the complete RFQ schema.

## Read first

- canonical `05-persistence-and-events.md`
- canonical `08-testing-and-seed.md`
- `00-technical-baseline.md`

## Implement

In `Rfq.Infrastructure`:

- EF Core `RfqDbContext`
- database connection configuration
- migration assembly configuration
- simple seed marker/master table sufficient to prove migrations/seeding
- design-time DbContext factory if required by `dotnet ef`

In `Rfq.DbTool` implement:

```text
migrate
seed
reset-dev
```

Behavior:

### `migrate`

Apply pending EF migrations.

### `seed`

Run idempotent development/demo seed operations.

For this step the seed may be minimal.

### `reset-dev`

Development-only:

1. drop/recreate local development DB, or otherwise clean it safely
2. apply all migrations
3. run seed

Require an explicit development environment / safety guard so this command cannot casually target a production-like database.

Create the initial migration.

Add Testcontainers infrastructure so a test can:

1. start PostgreSQL
2. apply migrations
3. verify the schema is reachable

## Do not implement

- full RFQ tables
- production deployment behavior
- application startup migration
- business seeding
- repository abstractions beyond what is needed to prove infrastructure

## Completion criteria

A fresh checkout can execute:

```bash
docker compose up -d
dotnet run --project src/Rfq.DbTool -- reset-dev
```

and end with a migrated/seeded local DB.

Running the command a second time remains predictable.

API startup performs no automatic migration/reset.

## Verify

```bash
dotnet build
dotnet run --project src/Rfq.DbTool -- reset-dev
dotnet run --project src/Rfq.DbTool -- migrate
dotnet run --project src/Rfq.DbTool -- seed
dotnet test tests/Rfq.Infrastructure.Tests
```

Inspect the DB once manually using a SQL client.

## Tests

Use real PostgreSQL through Testcontainers.

Verify:

- migrations apply from empty DB
- seed is idempotent
- DbTool safety rule for `reset-dev`

## Commit boundary

```text
add postgres migrations and db tool
```

<!-- END 02-database-foundation.md -->

---

<!-- BEGIN 03-rfq-create-and-list.md -->

# Step 03 — First Vertical Slice: Create and List RFQs

## Goal

Create the first end-to-end RFQ flow:

```text
browser -> API -> Application -> Domain -> PostgreSQL -> browser
```

A Sales user can create a minimal Draft RFQ and see it in a grid.

This is intentionally incomplete but fully runnable.

## Read first

- canonical `01-domain-model.md`
- canonical `03-use-cases-and-authorization.md`
- canonical Sales section of `04-ui-ux.md`
- canonical `05-persistence-and-events.md`

## Implement

### Domain

Introduce the minimum Case identity/value model required for:

- CaseId
- ClientId
- SecurityId
- CreatedAt
- CreatedBy
- CategorySnapshot if required structurally
- Draft lifecycle
- minimal initial `RfqRevision` in `Draft` status

Do not introduce all later quote states yet.

### Application

Add use cases/interfaces for:

- CreateDraft
- GetActiveSalesRfqs

Introduce `CurrentUser` abstraction, but use a simple development implementation in API for now.

Do not build full authorization policy yet.

### Persistence

Add the minimum tables/entities:

- `RfqCase`
- `CaseCurrent`
- `RfqRevision` with only the fields needed for an initial Draft at this stage

`Save Draft` must atomically create the Case and its initial Draft Revision, matching the canonical model. `CaseCurrent.CurrentRevisionId` may reference that Draft Revision while the Case lifecycle is Draft.

Create migration.

Keep table design consistent with the canonical final schema so later migrations evolve naturally.

### API

Add Controllers for:

- create draft
- list current RFQs

Keep DTOs explicit and simple.

### Frontend

Sales screen:

- AG Grid Community
- `New` action
- minimal creation form in left work pane or Ant Design drawer/panel
- fields for ClientId and SecurityId may temporarily be plain text identifiers in this step
- created row appears in grid after explicit reload/query
- basic loading/error state

Do not implement autocomplete yet.

## Important behavior

Unsaved `New` UI state is frontend-only until Save Draft is pressed.

Save Draft creates the Case and returns CaseId.

## Do not implement

- Confirm
- full Revision behavior beyond the initial Draft required by Save Draft
- quotes
- trader workflow
- search/autocomplete
- SSE
- background expiry
- bulk actions

## Completion criteria

From a clean DB:

1. open Sales screen
2. create Draft with ClientId + SecurityId
3. API persists the Case + initial Draft Revision atomically
4. reload browser
5. Draft remains visible

## Verify

Run:

```bash
dotnet test
npm test -- --run
npm run build
```

Manual:

- create two drafts
- refresh page
- confirm they are still present
- inspect DB rows

## Tests

Domain/Application:

- Case identity creation
- initial Draft Revision creation
- invalid missing Client/Security rejected

Infrastructure:

- create/read roundtrip against Testcontainers PostgreSQL

API:

- POST create then GET list

Frontend:

- create form calls API
- grid renders returned Draft

## Commit boundary

```text
add first rfq create and list vertical slice
```

<!-- END 03-rfq-create-and-list.md -->

---

<!-- BEGIN 04-master-search-and-defaults.md -->

# Step 04 — Client/Security Search and RFQ Defaults

## Goal

Replace raw identifier entry with usable Client/Security search and deterministic RFQ defaults **before Initial Confirm is introduced**.

## Read first

- canonical Security/Client Search in `06-calculation-and-search.md`
- standard settlement rules
- seed/master section in `08-testing-and-seed.md`

## Implement

### Master persistence

Add simple development master models/tables:

- User
- Desk
- Category
- CategoryRouting
- Client
- Security

This is mock/internal master data, not an authoritative firm-wide master system.

### Security search

Introduce `ISecuritySearch`.

Support the canonical input strategies:

- internal code normalization/search
- BBG-like text search
- ISIN/prefix search

Union/dedupe/rank results.

Return candidate fields useful to UI:

- SecurityId
- Japanese name
- BBG-like display
- Internal Code
- ISIN
- Category

### Client search

Simple code/name search -> ClientId.

### RFQ defaults

Implement:

```text
ResolveRfqDefaults(SecurityId, TradeDate)
```

returning at least:

- Category
- default Assigned Trader
- StandardSettlementDate

Also provide enough User master data for the Sales create form to resolve/display Contact Owner and Assigned Trader. For a Sales-created RFQ, default `ContactOwnerId` to the current user; Sales may override Assigned Trader before Confirm.

For now StandardSettlementDate may be deterministic mock logic hidden behind the calculation/default boundary.

### Frontend

Use Ant Design search/autocomplete behavior for Client/Security.

On Security selection:

- resolve defaults
- populate Standard Settlement
- default actual Settlement to Standard Settlement
- populate Category/Assigned Trader display as appropriate

Do not build a global application cache.

## Completion criteria

A user can create and save a Draft RFQ without knowing internal GUID/ID values, and all values required by the later Initial Confirm flow can be resolved/defaulted.

Search examples for internal code, ticker-like form, and ISIN prefix return sensible deterministic results.

## Tests

- normalization tests for each search grammar
- duplicate/ranking behavior
- default routing resolution
- API tests for search endpoints
- frontend search selection populates fields

## Commit boundary

```text
add master search and rfq defaults
```

<!-- END 04-master-search-and-defaults.md -->

---

<!-- BEGIN 05-sales-draft-and-confirm.md -->

# Step 05 — Sales Draft Revision and Initial Confirm

## Goal

Complete the Revision model and initial Sales flow now that Client/Security/default routing are available:

```text
New -> optional Save Draft -> Confirm -> Open / Requested / Initial
```

## Read first

- canonical Revision sections in `01-domain-model.md`
- initial-confirm transitions in `02-state-transitions.md`
- Sales editing behavior in `04-ui-ux.md`
- rationale sections 1, 2, 3, 11, 13 in `10-design-decisions.md`

## Implement

### Domain

Extend the minimal Step 03 `RfqRevision` into the canonical Revision model:

```text
RevisionStatus:
- Draft
- Confirmed
- Superseded
- Discarded
```

Revision-owned fields:

- Notional
- SettlementDate
- StandardSettlementDate
- SalesAndTradingMessage

Add initial Confirm transition so Case moves from Draft lifecycle to Open with:

```text
RfqStatus = Active
QuoteStatus = Requested
QuoteRequestReason = Initial
CurrentRevisionId = confirmed revision
```

### Persistence

Add `RfqRevision`.

Add constraints supporting:

- at most one Draft Revision per Case
- current revision FK
- optimistic version where appropriate

Create migration.

### Application/API

Implement:

- update initial Draft Revision
- confirm initial Revision
- discard initial draft behavior as defined by canonical design

For direct Confirm from unsaved frontend state, API may create Case + Revision atomically in one use case if that is cleaner.

### Frontend

Sales form now uses the resolved/default-capable fields from Step 04:

- Client
- Security
- Notional
- SettlementDate
- Sales & Trading Message
- Contact Owner
- Assigned Trader

On Security selection, use the Step 04 defaults to populate Category, Assigned Trader, StandardSettlementDate, and initial SettlementDate.

Support:

- Save Draft
- direct Confirm
- editing saved Draft
- status display

## Important invariant

Confirming a Revision must satisfy the canonical invariant immediately: a confirmed Revision has a WorkingQuote.

Create the minimal `WorkingQuote` persistence/model shell in this step and implement `EnsureWorkingQuote(revisionId)` for the empty/default case. Step 07 extends the same model with calculated/manual payloads and quote-edit behavior.

**Do not use a no-op temporary WorkingQuote ensurer.** Initial Confirm must create/ensure the WorkingQuote in the same atomic use case.

Initial Confirm validation includes resolved Client/Security, Notional > 0, valid SettlementDate >= business today, Contact Owner, and Assigned Trader.

## Do not implement

- amendment of already Open Case
- trader ownership operations
- calculation
- quote confirmation
- presentation

## Completion criteria

Manual journey:

1. New RFQ
2. enter fields
3. Save Draft
4. reload page
5. edit Draft
6. Confirm
7. row becomes Active + Requested
8. reload page
9. state persists

Also verify direct Confirm without prior Save Draft.

## Tests

Domain:

- Draft -> Open initial transition
- invalid Confirm rejected
- Requested requires Initial reason

Infrastructure:

- partial unique Draft Revision constraint
- confirmed Revision has exactly one WorkingQuote shell
- CurrentRevisionId FK and lifecycle/state persist consistently

API/Application:

- Save Draft
- Confirm initial
- direct Confirm path

Frontend:

- status change reflected after Confirm

## Commit boundary

```text
add revision model and initial rfq confirmation
```

<!-- END 05-sales-draft-and-confirm.md -->

---

<!-- BEGIN 06-trader-routing-and-ownership.md -->

# Step 06 — Trader Routing and Ownership

## Goal

Create the Trader active RFQ screen and implement ownership/routing operations.

No pricing yet.

## Read first

- ownership section of `01-domain-model.md`
- ownership transitions in `02-state-transitions.md`
- authorization section in `03-use-cases-and-authorization.md`
- Trader layout in `04-ui-ux.md`

## Implement

### Domain/Application

Represent:

- AssignedTraderId
- Owned

Rule:

```text
Owned = true => owner is AssignedTraderId
```

Implement operations:

- Pick Up
- Release
- Assign To
- Take Over

Follow canonical preconditions and confirmation semantics.

Add the first centralized `IRfqAuthorization` implementation.

Retrofit the existing Revision create/edit/confirm/discard handlers from Steps 03-05 to use this centralized authorization boundary. After this step, Controllers/handlers must not retain ad-hoc authorization checks for those existing operations.

CurrentUser development identity should be configurable so local testing can switch between at least:

- Sales user
- Trader A
- Trader B

Do not scatter authorization conditionals through Controllers.

### Persistence

Persist routing/ownership changes in `CaseCurrent`.

Add concurrency version if not already present.

Record audit events only if event persistence already exists; otherwise keep an explicit application seam and add events in Step 12. Do not build half an event system here.

### API

Trader active list endpoint.

Ownership command endpoints.

### Frontend

Trader screen:

- Active RFQ AG Grid
- Assigned Trader
- Owned indication
- row selection
- Pick Up / Release / Assign To / Take Over actions
- confirmation dialogs where canonical design requires them

No quote editing columns yet.

## Completion criteria

Using two local trader identities:

1. unowned assigned RFQ can be picked up
2. owner can release
3. unowned RFQ can be assigned
4. another trader can take over an owned RFQ after confirmation
5. invalid operations return clear Forbidden/Conflict behavior

## Tests

Domain/Application authorization and transitions.

API permission tests.

At least one concurrency test for two traders attempting ownership change.

## Commit boundary

```text
add trader routing and ownership workflow
```

<!-- END 06-trader-routing-and-ownership.md -->

---

<!-- BEGIN 07-working-quote-and-calculation.md -->

# Step 07 — WorkingQuote and Mock Calculation

## Goal

Add the trader's editable pricing workflow backed by a Revision-owned WorkingQuote and deterministic mock calculation.

Do not Confirm the quote yet.

## Read first

- WorkingQuote section in `01-domain-model.md`
- quote calculation model in `06-calculation-and-search.md`
- calculation failure logging in `05-persistence-and-events.md`
- Design Decisions 10 and 11

## Implement

### Domain

Add Revision-owned `WorkingQuote`.

Support:

- Calculated mode
- Manual mode
- current calculated payload
- current manual payload
- optimistic version

Implement `EnsureWorkingQuote(revisionId)` behavior:

1. return existing
2. clone configured seed Revision WorkingQuote if specified
3. otherwise create empty/default

### Calculation boundary

Create:

```text
ICalculationClient
MockCalculationClient
```

Use the canonical bulk request/response shape concept:

- requestId
- securityId
- settlementDate
- calculation type / driver
- typed parameter

Responses are per-item Success/Error.

Mock must be deterministic and plausibly shaped, but need not be financially correct.

Calculated flow must support:

- edited cell implies driver
- base Simple Yield
- trader-entered `SimpleYieldSlide`
- final Simple Yield = base + slide
- representative output fields needed by grid

### Failure behavior

Calculate before short DB write transaction.

Before writing a successful result, re-read/revalidate the command-side current state: expected `CaseCurrent` version/current Revision, owner authorization, and WorkingQuote version must still match the state against which calculation was requested. A calculation started for an old Revision or old owner must not write back after Sales confirms a new Revision or ownership changes.

On calculation failure:

- WorkingQuote remains unchanged
- return typed CalculationFailure error
- persist `CalculationFailureLog`
- frontend reverts edit and shows toast

### Manual mode

Switching to Manual:

- clears Manual values
- retains Calculated state

Switching back restores retained Calculated state.

Manual mode has independent:

- Price
- Final Simple Yield

Do not enforce consistency between them.

### Frontend

Trader grid gains editable quote columns.

Only owner trader can edit.

Add mode switch and a visible calculation status/error behavior.

## Completion criteria

Manual:

1. pick up RFQ
2. edit Price -> calculated outputs change
3. edit another driver -> correct driver is sent
4. edit Slide -> final simple yield changes
5. force mock failure -> cell reverts and failure toast appears
6. switch Manual -> values blank
7. enter manual values
8. switch back -> previous calculated state returns

## Tests

- EnsureWorkingQuote
- mode switching
- calculation success update
- calculation failure leaves DB WorkingQuote unchanged
- optimistic WorkingQuote version conflict
- calculation result rejected if CurrentRevision changed while calculation was in flight
- calculation result rejected if ownership/CaseCurrent version changed while calculation was in flight
- failure log persisted

## Commit boundary

```text
add working quote and mock calculation workflow
```

<!-- END 07-working-quote-and-calculation.md -->

---

<!-- BEGIN 08-confirm-and-present-quote.md -->

# Step 08 — ConfirmedQuote and Presentation

## Goal

Allow Trader to Confirm a WorkingQuote into an immutable ConfirmedQuote, then allow Contact Owner to Present/Unpresent it.

## Read first

- ConfirmedQuote and Presentation sections in `01-domain-model.md`
- quote/presentation transitions in `02-state-transitions.md`
- authorization rules
- Design Decisions 4 and 14

## Implement

### ConfirmedQuote

Persistence supports Revision -> 0..N immutable ConfirmedQuotes.

Quote Confirm:

- requires lifecycle Open
- requires `QuoteStatus = Requested`
- requires owner trader
- requires WorkingQuote belonging to the current Revision
- requires expected WorkingQuote/CaseCurrent versions to match
- requires no currently valid current ConfirmedQuote
- does **not** recalculate
- snapshots current WorkingQuote
- stores calculation/manual mode and required context
- sets current quote reference
- emits/persists a `QuoteEvent: Confirmed` once event persistence exists; until Step 12, keep an explicit application event seam so Step 12 can backfill this transition without changing its business behavior
- updates:
  - QuoteStatus = Quoted
  - QuoteRequestReason = null

For expiry selection, store enough data to support Step 11:

- Trader default expiry setting (None or N minutes) in development User/preferences data
- per-RFQ override before Confirm
- expiry setting captured on ConfirmedQuote
- resolved ExpiresAt when applicable

### Presentation

Contact Owner can:

```text
Active + Quoted -> Presented + Quoted
Presented + Quoted -> Active + Quoted
```

Trader cannot Withdraw while Presented, although Withdraw itself arrives in Step 11.

### Frontend

Trader:

- Confirm Quote action
- expiry selector: None or N minutes
- clear visual indication of current confirmed quote
- quote cells locked while `QuoteStatus = Quoted`; they become editable again only after Revised/Reopened/Expired/Withdrawn returns the RFQ to Requested

Sales/Contact Owner:

- Present
- Unpresent

Do not imply Present means customer technically received anything.

## Completion criteria

1. Trader calculates WorkingQuote
2. Confirm creates immutable snapshot
3. changing WorkingQuote later does not mutate old ConfirmedQuote
4. Contact Owner Presents
5. status becomes Presented
6. Unpresent returns to Active/Quoted

## Tests

- Confirm does not invoke calculation client
- immutable historical quote remains unchanged
- only owner trader confirms
- only Contact Owner Presents/Unpresents
- Present requires current valid quote
- second Confirm while a valid current quote is already active is rejected
- Quoted WorkingQuote is not editable

## Commit boundary

```text
add quote confirmation and presentation
```

<!-- END 08-confirm-and-present-quote.md -->

---

<!-- BEGIN 09-hit-away-and-contact-owner.md -->

# Step 09 — Hit/Away, Contact Owner, and Memos

## Goal

Complete the normal happy-path RFQ lifecycle:

```text
Sales create -> Trader quote -> Present optional -> Hit/Away
```

and add Contact Owner handoff plus case memos.

## Read first

- Contact Owner, Close, Memo sections in `01-domain-model.md`
- close transitions in `02-state-transitions.md`
- authorization matrix
- EOD notes only for context; EOD UI comes later

## Implement

### Close

Contact Owner may close an Open RFQ with a valid current quote:

- Hit
- Away

Presentation is not required.

On close:

- lifecycle -> Closed
- RfqStatus -> Hit or Away
- `ClosedQuoteId` records quote in `CaseCurrent`
- current operational quote association is no longer used for open workflow
- Owned -> false
- pending Draft Revision -> Discarded
- historical quotes/WorkingQuote retained

Add explicit outcome correction:

```text
Hit <-> Away
```

No silent rewriting during bulk operations.

### Contact Owner

Support changing Contact Owner between valid Sales/Trader users.

Require explicit confirmation in UI.

### Memos

Add Case-level:

- Sales-only Memo
- Trader-only Memo

They remain editable after close according to role.

Do not confuse these with Revision-owned SalesAndTradingMessage.

### Frontend

Sales/Trader details expose appropriate memo.

Contact Owner controls:

- Hit
- Away
- Bulk Hit / Bulk Away for selected rows, with per-item results; already Closed rows are skipped and never have their outcome silently changed
- Contact Owner handoff
- outcome correction when Closed

## Completion criteria

Complete a full RFQ from create through Hit and another through Away.

Verify a closed RFQ cannot be operated as Open.

Verify memo permissions and post-close editing.

## Tests

- close preconditions
- ClosedQuoteId
- pending Draft discard
- ownership cleared
- outcome correction
- bulk close skips already Closed rows and reports per-item result
- Contact Owner authorization
- role-scoped memo editing

## Commit boundary

```text
complete hit away and contact owner workflow
```

<!-- END 09-hit-away-and-contact-owner.md -->

---

<!-- BEGIN 10-amendment-and-requote.md -->

# Step 10 — Amendment and Requote

## Goal

Implement amendment without introducing an `Amending` RFQ status.

## Read first

- Revision behavior in `01-domain-model.md`
- amendment transitions in `02-state-transitions.md`
- Sales UX in `04-ui-ux.md`
- Design Decisions 2 and 13

## Implement

For an Open RFQ:

- editing Revision-owned fields creates/updates one Draft Revision
- current Confirmed Revision remains operational
- current quote remains operational while Draft exists
- Trader cannot see Draft contents
- Sales sees changed-cell indication

On amendment Confirm:

- previous current Confirmed -> Superseded
- Draft -> Confirmed
- CurrentRevisionId changes
- ensure WorkingQuote for new Revision
- RfqStatus -> Active
- QuoteStatus -> Requested
- QuoteRequestReason -> Revised
- current quote association removed
- if previously Presented, no longer Presented

On Discard:

- Draft -> Discarded
- current Confirmed Revision remains current
- live quote/status remain unchanged

Implement copying a historical condition into a new Revision where reasonable:

- `CopiedFromRevisionId`
- `QuoteSeedRevisionId`

Do not reactivate an old Revision.

### Create New from Existing

Implement the canonical `CreateFromExisting` use case. It always creates a new Case.

Copy:

- Client
- Security
- Notional
- Sales & Trading Message

Do not copy Sales-only Memo, Trader-only Memo, quote lifecycle/state, or old ownership.

Initialize Contact Owner/Sales/routing as a new Case, rerun Assigned Trader routing, apply the canonical settlement rule (source created today -> copy actual settlement; older source -> current standard settlement), and persist `CopiedFromCaseId` as provenance only.

## Frontend

Sales grid/edit pane:

- immediate server autosave after committed cell edit on existing Open RFQ
- visual highlight of changed cells
- Confirm Amendment
- Discard Amendment
- Confirm Selected / Discard Selected with per-Case results; one conflict must not fail unrelated selected Cases
- Create New from Existing

Trader view continues to show only current Confirmed data until Confirm.

## Completion criteria

Manual:

1. start with Quoted RFQ
2. Sales edits Notional
3. Trader still sees old confirmed Notional/quote
4. Sales Discards -> no status change
5. Sales edits again and Confirms
6. Trader sees new Revision
7. QuoteStatus becomes Requested/Revised
8. WorkingQuote exists for new Revision

## Tests

- only one Draft per Case
- Draft does not affect current quote
- Confirm supersedes old Revision atomically
- Discard behavior
- quote seeding from historical Revision
- bulk Confirm/Discard partial success
- CreateFromExisting copy/non-copy/routing/settlement rules

## Commit boundary

```text
add amendment and requote workflow
```

<!-- END 10-amendment-and-requote.md -->

---

<!-- BEGIN 11-cancel-reopen-withdraw-expiry.md -->

# Step 11 — Cancel, Reopen, Withdraw, and Expiry

## Goal

Complete the remaining major lifecycle transitions and automatic quote expiry.

## Read first

- Cancel/Reopen/Withdraw/Expiry sections in canonical design
- `02-state-transitions.md`
- `07-runtime-and-notifications.md`
- Design Decisions 8, 9, and 12

## Implement

### Withdraw

Trader operation.

Allowed when:

- QuoteStatus = Quoted
- RFQ is not Presented

Effects:

- RfqStatus = Active
- QuoteStatus = Requested
- QuoteRequestReason = Withdrawn
- current quote association removed
- WorkingQuote retained

Support multi-select command semantics; Presented rows are skipped/reported, not silently mutated.

### Cancel

Open -> Cancelled.

Retain:

- Assigned Trader
- Contact Owner
- WorkingQuote
- ConfirmedQuote history
- Draft amendment may remain Draft

Set Owned=false.

### Reopen

Cancelled -> Open:

```text
RfqStatus = Active
QuoteStatus = Requested
QuoteRequestReason = Reopened
Owned = false
```

Do not resurrect old ConfirmedQuote as current.

Retain WorkingQuote values.

### Expiry

Implement ASP.NET Core `BackgroundService`.

Initial assumption:

- one App Server instance
- approximately 10-second interval

Find currently quoted rows with `ExpiresAt <= now`.

Call the normal Application `ExpireQuote` use case.

Expiration must be:

- idempotent
- concurrency-safe

Effects:

- Presented -> Active if needed
- Quoted -> Requested
- reason -> Expired
- current quote association removed
- WorkingQuote retained

Do not implement leader election/distributed worker logic.

## Completion criteria

Manual:

- Withdraw ordinary quote
- verify Presented quote cannot Withdraw
- Cancel + Reopen preserves working values but requests quote again
- Confirm a quote with 1-minute expiry and observe automatic expiration

For faster automated/manual development tests, a configurable shorter expiry/worker interval is acceptable only in Development configuration.

## Tests

- all transition preconditions
- idempotent expiry
- concurrent expiry/manual operation
- Reopen does not restore current quote
- Withdraw Presented rejection

## Commit boundary

```text
add cancel reopen withdraw and expiry
```

<!-- END 11-cancel-reopen-withdraw-expiry.md -->

---

<!-- BEGIN 12-events-refresh-and-sse.md -->

# Step 12 — Persisted Events, Refresh Windows, and SSE Wake-up

## Goal

Add reliable change notification without auto-refreshing editable grids.

## Read first

- event persistence in `05-persistence-and-events.md`
- notification/runtime behavior in `07-runtime-and-notifications.md`
- Changes UX in `04-ui-ux.md`
- Design Decisions 5, 6, 7, 15, 16, 17, 18

## Implement

### Persistence

Add shared parent:

```text
Event
- EventId
- OccurredAt
- ActorUserId?
```

Child tables:

```text
RfqEvent
- EventId PK/FK
- CaseId
- Type
- Payload jsonb

QuoteEvent
- EventId PK/FK
- QuoteId
- Type
- Payload jsonb
```

Domain does not need common inheritance.

Add typed payloads in Domain/Application and serialize them in Infrastructure.

Initial event coverage should include canonical important events, including at least:

RFQ:

- RevisionConfirmed
- Cancelled
- Reopened
- ClosedHit
- ClosedAway
- OutcomeCorrected
- ContactOwnerChanged
- TakenOver
- PickedUp / Released / AssignedTraderChanged where those changes should appear to another active Trader session

Quote:

- Confirmed
- Presented
- Unpresented
- Withdrawn
- Expired

Backfill command implementations so state mutation + event append are in the same transaction.

Rule: every state mutation that another active session must be able to discover through Pending Updates/reconnect must have a persisted event. Do not rely on SSE-only wake-ups or in-memory notifications for such transitions.

### Event query

Implement:

```text
GetEventsAfter(EventId)
```

with appropriate user/screen filtering for notification relevance.

### Cursor correctness under concurrent commits

The EventId/cursor mechanism must be **commit-order safe**. Do not use a plain PostgreSQL identity/sequence as the sole cursor guarantee: transaction A can allocate a lower ID, transaction B can allocate/commit a higher ID, the client can advance past B, and A can then commit invisibly below the cursor.

Use a DB-serialized cursor allocator (for example a single-row counter locked until commit) or another mechanism that provides the same no-gap property. Keep this mechanism in Infrastructure; Domain/Application only see EventIds/events.

### SSE

SSE sends only a lightweight `changed` wake-up.

On wake-up, frontend fetches events after last EventId.

SSE is not the authoritative state payload.

### Frontend

Add:

```text
Updates Available
```

indicator.

Do **not** reload main grid automatically.

Implement Changes tab with:

- Last Refresh
- Pending Updates

Use cursor boundaries conceptually:

- PreviousRefreshEventId
- LastRefreshEventId
- LatestSeenEventId

On Refresh:

- reload authoritative grid data
- Pending becomes Last Refresh
- clear Pending

Important event toasts only for selected canonical events.

## Completion criteria

Open two browser sessions/users.

Cause a state change in one.

Other session:

- receives Updates Available
- main grid does not silently change
- Pending Updates shows event
- pressing Refresh updates grid
- event moves to Last Refresh

Disconnect/reconnect SSE and verify persisted event fetch catches up.

## Tests

- state + event transactionality
- global EventId ordering/uniqueness
- **reverse commit-order concurrency test** proving a client cannot miss an event when two event-producing transactions complete in the opposite order from their initial work
- child event FK structure
- GetEventsAfter cursor behavior
- SSE endpoint smoke test
- frontend refresh-window reducer/state logic

## Commit boundary

```text
add persisted events and explicit refresh notifications
```

<!-- END 12-events-refresh-and-sse.md -->

---

<!-- BEGIN 13-past-search-eod-pricer-grid-config.md -->

# Step 13 — Past Search, EOD, Pricer, and Grid Configuration

## Goal

Add the secondary operational surfaces after the primary RFQ lifecycle works.

## Read first

- Past RFQ / EOD / Pricer / Grid config sections in `04-ui-ux.md`
- search notes in `06-calculation-and-search.md`

## Implement

### Past RFQ search

Trader lower panel defaults to Past RFQ.

One Case = one row.

Server-side filters:

- date range
- Client
- Security
- Category
- Contact Owner
- Sales
- Assigned Trader
- outcome/status
- quote state if useful
- CaseId

Return up to approximately 20k rows.

If result exceeds cap, return a clear "narrow search" response.

Frontend uses AG Grid client-side sort/filter on returned rows.

Do not implement infinite scroll yet.

### Revision / Quote history

Implement the canonical query-side history surfaces:

- `GetRevisionHistory(CaseId)`
- `GetQuoteHistory(CaseId)`

These are read-model queries and must not reconstruct the full command aggregate. Show immutable/superseded/discarded Revision history and ConfirmedQuote history needed for operational investigation.

### EOD

Separate tab.

Summary:

```text
Contact Owner | Open | Hit | Away
```

Open count drills into unclosed RFQs for that owner.

Do not grant desk-wide Hit/Away authority merely because data is visible here.

### Pricer

Independent scratch state in right-side drawer.

Works:

- from selected RFQ: copy values into scratch state
- without selection: arbitrary Security/Notional
- uses mock CalculationClient-backed API
- not auto-synchronized after opening

No Apply-back to RFQ.

### Grid config

Persist:

```text
UserGridConfig
- UserId
- ScreenId
- ConfigKey
- Version
- ConfigJson
- UpdatedAt
```

Store:

- visibility
- order
- width
- pinning
- optionally sort/filter

Add an application-controlled config migration/version hook so column ID changes can be handled later.

## Completion criteria

- Past search is useful against seeded history
- Revision and Quote history are inspectable for a selected Case
- >20k query produces narrowing message
- EOD counts agree with DB
- scratch Pricer works without RFQ
- grid layout survives browser restart because it is server-stored

## Tests

Query integration tests with PostgreSQL, including Revision/Quote history ordering and content.

Grid config roundtrip/version test.

Pricer independent-state frontend test.

## Commit boundary

```text
add history eod pricer and grid preferences
```

<!-- END 13-past-search-eod-pricer-grid-config.md -->

---

<!-- BEGIN 14-concurrency-errors-and-integration-tests.md -->

# Step 14 — Concurrency, Errors, and Integration Hardening

## Goal

Make the already-built workflows behave predictably under stale edits, concurrent users, and representative API failures.

This step should not add new business features.

## Read first

- concurrency/error sections of `03-use-cases-and-authorization.md`
- persistence version rules
- full lifecycle transitions

## Implement

### Optimistic concurrency

Ensure versioning exists where needed:

- CaseCurrent
- RfqRevision where mutable
- WorkingQuote

ConfirmedQuote remains immutable and does not need mutable concurrency version.

On conflict:

- do not auto-merge
- API returns stable Conflict error/code
- frontend reloads affected RFQ only
- stale edit is discarded
- show clear toast

Bulk operations:

- per-item result
- conflicting/invalid rows fail independently

### Error contract

Use stable categories:

- Validation
- Conflict
- Forbidden
- NotFound
- CalculationFailure

Map to consistent HTTP responses.

Avoid a huge generic framework.

### Integration coverage

Expand Testcontainers tests around:

- Revision partial unique constraint
- ownership races
- quote confirm races
- calculation write-back vs Revision Confirm / ownership change
- expiry race
- close vs amend race
- transaction rollback if event append/persistence fails
- event cursor reverse-commit-order no-loss test
- JSONB event serialization
- important search queries

### API journey tests

Automate at least:

1. Sales create -> Trader quote -> Present -> Hit
2. revise -> requote -> Away
3. expiry -> Requested/Expired -> requote
4. cancel -> reopen -> requote
5. stale version conflict

## Completion criteria

All representative race/error cases are deterministic.

No happy-path endpoint silently overwrites newer state.

## Verify

```bash
dotnet test
cd src/Rfq.Web
npm test -- --run
npm run build
```

Also perform one manual two-browser concurrency test.

## Commit boundary

```text
harden concurrency errors and integration coverage
```

<!-- END 14-concurrency-errors-and-integration-tests.md -->

---

<!-- BEGIN 15-seed-data-and-demo-readiness.md -->

# Step 15 — Seed Data and Demo Readiness

## Goal

Produce a convincing local/demo environment without adding production-only architecture.

## Read first

- `08-testing-and-seed.md`
- canonical scope/deferred list

## Implement

### Masters

Seed:

- several Sales users
- several Traders
- desk/category/routing combinations
- Clients
- meaningful Security universe

Where practical, seed Security-shaped data inspired by public Japanese bond reference data, but do not attempt to recreate an authoritative market master.

Fake/generated fields are acceptable for:

- internal IDs
- ISINs
- ticker-like displays
- categories

Generated ISINs should be structurally valid if validation exists.

### Historical RFQs

Generate hundreds to thousands of synthetic Cases spanning:

- Draft
- Active/Requested
- Active/Quoted
- Presented
- Cancelled
- Hit
- Away
- revisions
- historical quotes
- expiry/withdrawal examples

Include enough data to exercise Past Search and EOD.

Provide an optional larger seed profile for performance checks.

### Developer UX

Document exact commands:

```bash
docker compose up -d
dotnet run --project src/Rfq.DbTool -- reset-dev
dotnet run --project src/Rfq.Api
cd src/Rfq.Web
npm install
npm run dev
```

Provide sample local identities and demo journeys.

### Final cleanup

Review for accidental implementation of explicitly deferred items and remove/disable speculative UI where appropriate.

Do not add:

- production auth transport
- external Bloomberg integration
- real calculation server
- distributed runtime
- New Bulk/List/Thread
- Manager override workflow
- Pricer Apply-back

## Completion criteria

A new developer can clone, reset-dev, start API/UI, choose a documented local identity, and execute the main demo flows without hand-editing the DB.

Past search and EOD contain enough data to look realistic.

## Tests

Seed idempotency after reset.

Basic invariant validation over generated dataset.

Optional performance smoke test for Past RFQ query.

## Commit boundary

```text
add demo seed data and local runbook
```

<!-- END 15-seed-data-and-demo-readiness.md -->
