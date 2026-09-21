# Business date and EOD correctness

## Summary

Implemented `docs/refactorings/02-business-date-and-eod-correctness.md` without changing
Domain state, the API contract, or UTC timestamp storage.

## Past RFQ search

- Resolves the current user's desk timezone from the desk master when a date filter is present.
- Interprets `From` and `To` as calendar dates in that desk timezone.
- Converts local midnight boundaries to UTC before applying the database predicates.
- Uses a half-open interval: `From` is inclusive and the midnight after `To` is exclusive.
- Rejects a nonexistent local-midnight boundary instead of silently applying an incorrect offset.

## EOD

- Treats EOD as a current desk-wide remaining-work view rather than a historical snapshot.
- Removes the RFQ `CreatedAt` date restriction.
- Restricts rows to RFQs assigned to Traders on the current user's desk.
- Aggregates current `Active` / `Presented` as Open and current `Hit` / `Away` as their
  corresponding outcome counts.
- Excludes lifecycle states that contribute no remaining-work or outcome count.

## Verification coverage

- JST local-midnight boundaries and their corresponding UTC instants.
- Inclusion of the full `To` date and exclusion at the following local midnight.
- UTC dates that differ from the desk-local calendar date.
- Inclusion of older open RFQs in EOD.
- Current Hit/Away aggregation and exclusion of another desk's RFQs.
