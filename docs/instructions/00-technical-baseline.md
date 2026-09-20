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
