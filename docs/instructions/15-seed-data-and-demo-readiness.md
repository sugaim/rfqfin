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
