# Type safety and repository contracts

## Summary

Implemented `docs/refactorings/01-type-safety-and-repository-contracts.md` without changing
the database schema or public HTTP JSON shapes.

## Changes

- Added the `DeskId` Domain value type and used it throughout Application identity,
  business-date, user-directory, and Trader repository contracts.
- Kept desk columns as strings in EF entities and converted with `DeskId.Value` / `DeskId.Create`
  at Infrastructure boundaries.
- Changed `PersistedEvent` identifiers to `CaseId`, nullable `UserId`, and—on quote events—required
  `QuoteId`.
- Changed persisted RFQ and Quote event `Type` values from raw strings to
  `RfqTransitionKind` and `QuoteTransitionKind`. Infrastructure parses DB strings strictly and
  rejects unknown or undefined values; the API maps the enums back to the existing strings.
- Added typed RFQ/Quote persisted-event variants and `PersistedEventKind`; Application and
  Infrastructure no longer use `"Rfq"` / `"Quote"` string branching.
- Replaced the nullable-ID `PendingEvent` record with `PendingRfqEvent` and `PendingQuoteEvent`.
  Each variant requires exactly the identifier needed by its persistence target.
- Removed the default `IRfqCaseRepository.GetExpiredQuotesAsync` implementation. Production and
  test repositories now implement it explicitly.
- Preserved the existing events API response by mapping typed Application events back to primitive
  response fields in the Controller.

## Verification coverage

- `DeskId` normalization and empty-value rejection.
- Pending event variants retain target-specific typed identifiers.
- Persisted quote events expose typed case, actor, and quote identifiers.
- Existing build and test suites.
