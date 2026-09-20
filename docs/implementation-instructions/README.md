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
