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

The current bootstrap exposes `GET /api/health` and OpenAPI at
`/openapi/v1.json`. Database commands are placeholders until implementation
step 02; list them with:

```bash
dotnet run --project src/Rfq.DbTool -- --help
```
