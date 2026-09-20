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
