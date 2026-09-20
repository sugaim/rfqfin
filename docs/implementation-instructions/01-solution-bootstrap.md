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
