# 2026-09-21 - Domain Refactor Follow-up

This follow-up incorporates the Domain structure and responsibility review after the
semantic refactor.

## Domain organization

- Relaxed the mechanical one-top-level-type-per-file rule for small, closed Domain
  type families.
- Colocated lifecycle, open-RFQ, quote-state, ownership, WorkingQuote, and quote-expiry
  families with their major concepts.
- Grouped Domain transitions by business category.
- Kept transition result types beside the transition group that produces them.
- Preserved the stable `Rfq.Domain` namespace.

## Domain responsibility corrections

- `WorkingQuoteFactory.CreateInitialFor` now accepts only an Active RFQ with
  `QuoteRequested(Initial)` and a confirmed current Revision.
- `RfqOwnershipTransitions.Release` no longer accepts or checks an acting Trader. It
  validates only Open + Owned, changes ownership to Unowned, and preserves the
  Assigned Trader.
- `RfqOwnershipTransitions.TakeOver` no longer performs the actor-relative
  same-Trader check. Application authorization retains that policy.

## Compatibility and deferred work

- The Application Release use case was adapted only to call the revised Domain API;
  its existing authorization check remains in place.
- HTTP contracts, frontend behavior, database schema, migrations, persisted events,
  calculation behavior, and lifecycle behavior are unchanged.
- `IWorkingQuoteEnsurer`, query-side WorkingQuote repair, Application primitive
  values, and optional Sales ownership remain deferred to a later pass.
