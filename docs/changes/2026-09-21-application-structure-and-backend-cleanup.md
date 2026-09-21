# Application structure and backend cleanup

## Summary

The Application structure follow-up and the remaining backend cleanup were applied together.
HTTP request and response shapes remain primitive at the API boundary; Application code now uses
Domain identifiers, versions, and status enums after that boundary.

## Application structure

- Reorganized `Rfq.Application` by feature under `Rfqs`, `Quotes`, and `Queries`.
- Kept major use cases and abstractions independently discoverable.
- Co-located small closed request/result/value families in feature model files instead of applying
  a mechanical one-public-type-per-file rule.
- Moved contact-owner responsibility handling out of the lifecycle folder.
- Kept the stable `Rfq.Application` namespace despite the deeper folder tree.

## Typed Application boundary

- Replaced internal primitive RFQ, revision, quote, user, client, security, category, and state
  version values with their Domain types.
- Replaced internal status strings with Domain enums where the value represents business state.
- Added explicit Controller mappings so existing JSON continues to expose numbers, GUIDs, and
  strings rather than Domain wrapper objects.
- Kept primitives for genuine transport and persistence concerns such as HTTP DTOs and persisted
  event rows.

## WorkingQuote invariant

- Removed `IWorkingQuoteEnsurer`, its Infrastructure implementation, and its DI registration.
- Removed query-side WorkingQuote creation from the Trader active-RFQ query.
- Initial confirmation, confirm-new, and amendment confirmation remain the only normal lifecycle
  paths that create the required WorkingQuote in the same unit of work.
- A missing WorkingQuote on a Trader row is now reported as a `DomainInvariantException`; reads do
  not silently mutate database state.

## Nullable SalesId

- Changed `RfqCase.SalesId` and the persistence model to nullable.
- Sales-created RFQs retain the creating Sales user as `SalesId`.
- Trader-created RFQs store `SalesId = null` while the Trader remains the creator and Contact Owner.
- Added migration `20260921105327_MakeSalesIdNullable`.
- Added a development seed example for a Trader-created RFQ.

## Other cleanup

- Required event sinks and time providers are no longer modeled as optional where the workflow
  always depends on them.
- Removed architecture-level null-forgiving fallbacks from the changed Application paths.
- Preserved persisted event payloads as primitive values after event models became typed.

## Verification coverage

- Sales-created and Trader-created RFQ ownership/nullability behavior.
- Initial-confirm, confirm-new, and amendment-confirm WorkingQuote creation and single-commit behavior.
- Trader-screen reads perform no write, including the missing-WorkingQuote invariant path.
- Nullable `SalesId` database round-trip and migration behavior.
- Existing Domain, Application, Infrastructure, and API suites.
