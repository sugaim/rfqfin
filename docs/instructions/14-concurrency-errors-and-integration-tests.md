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
