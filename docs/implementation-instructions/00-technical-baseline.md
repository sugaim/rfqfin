# Technical Baseline

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

Use compatible patch releases within these majors.

Do not target .NET 10/11 for this project baseline.

---

## Domain type style

Use ordinary C# types/records, not preview union syntax.

Conceptually:

```text
RfqLifecycle
├─ DraftRfq
├─ OpenRfq
│  ├─ ActiveRfq
│  │  ├─ QuoteRequested
│  │  └─ QuoteConfirmed
│  └─ PresentedRfq
├─ CancelledRfq
└─ ClosedRfq
```

Domain business-state values are immutable from callers.

Use typed IDs/value objects, including `StateVersion`.

Persistence may flatten typed state into EF columns.

---

## Date/time baseline

Use `DateOnly` for business dates and UTC timestamps for instants.

Business `today` is resolved using configured desk/business timezone (initially JST / `Asia/Tokyo`), not UTC calendar date.

---

## Database

```text
PostgreSQL
EF Core migrations
```

Migrations live with Infrastructure.

API must not auto-run migrations/seed.

`Rfq.DbTool` provides local/deployment-support commands such as:

```text
migrate
seed
reset-dev
```

---

## Frontend

Current frontend baseline remains:

```text
Node 24 LTS
React 19.3
TypeScript 5.8+
Vite 8 compatible
Ant Design 6.x
AG Grid Community 36.x
Redux Toolkit 2.x / RTK Query
npm
```

The backend semantic refactor does not require frontend refactoring.

---

## Version locking / compiler settings

Continue using:

```text
global.json
Directory.Packages.props
Directory.Build.props
```

`Nullable` and implicit usings are enabled.

The intended code-quality baseline also includes warnings-as-errors plus `.editorconfig` / `dotnet format`, but the solution-wide hygiene pass is a **separate commit** from the semantic domain refactor to keep reviewable diffs. Do not mix unrelated formatting/analyzer churn into the semantic commit.

---

## Development runtime

Local PostgreSQL runs via Docker Compose.

Normal components:

```text
PostgreSQL   -> Docker Compose
ASP.NET API  -> Visual Studio 2022 or dotnet run
React/Vite   -> npm run dev
```

---

## OpenAPI

ASP.NET Core implementation is authoritative.

Generate OpenAPI from server DTO/endpoints.

Do not begin a separate contract-first specification solely for this refactor.
