# RFQ

Internal JPY corporate bond RFQ application.

## Prerequisites

- Visual Studio 2022 with the .NET 9 SDK
- Node.js 24 LTS and npm
- Docker Desktop with Docker Compose

## Local bootstrap

Start PostgreSQL:

```bash
docker compose up -d
```

Reset the guarded local development database, apply migrations, and seed it:

```bash
dotnet run --project src/Rfq.DbTool -- reset-dev
```

Restore and build the backend:

```bash
dotnet restore
dotnet build
```

Start the API at `http://localhost:5100`:

```bash
dotnet run --project src/Rfq.Api
```

In another terminal, start the frontend at `http://localhost:5173`:

```bash
cd src/Rfq.Web
npm install
npm run dev
```

The API exposes `GET /api/health` and OpenAPI at `/openapi/v1.json`.
Database commands are available through:

```bash
dotnet run --project src/Rfq.DbTool -- --help
```

`reset-dev` is intentionally guarded: it requires the Development environment
and accepts only the local database named `rfq`. The default DbTool launch
profile and Docker Compose connection satisfy those checks.

## Demo identities and data

Use the identity switcher in the header (or the `X-Development-User` header):

- `sales-dev`: primary Sales journey and Contact Owner
- `sales-a`: second Sales user for handoff/concurrency demonstrations
- `trader-a`: JGB/OTHER Trader
- `trader-b`: corporate-bond Trader

`reset-dev` idempotently seeds masters plus 750 RFQs across Draft, Requested,
Quoted, Presented, Cancelled, Hit, and Away states. Set
`RFQ_SEED_PROFILE=large` before `reset-dev` to create 5,000 RFQs.

Suggested journeys:

1. As `sales-dev`, create and confirm an RFQ; as its routed Trader, pick it up,
   calculate and confirm a quote; return as Sales to Present and close Hit/Away.
2. Edit an Open RFQ to create an amendment Draft. Observe that the Trader still
   sees the confirmed revision, then Confirm the amendment and requote.
3. Withdraw a non-presented quote, or Cancel then Reopen it. Expiring quotes are
   detected by the development worker every two seconds.
4. Use Past RFQ, history, EOD and the independent Pricer to inspect the seeded set.

The local stack deliberately uses development identity headers, mock pricing,
one application server, and no external market-data integration.
