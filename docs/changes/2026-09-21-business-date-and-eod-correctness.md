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

- Combines a current desk-wide Open count with close events from the selected desk-local date;
  it does not rebuild a historical snapshot.
- Removes the RFQ `CreatedAt` date restriction.
- Restricts rows to RFQs assigned to Traders on the current user's desk.
- Aggregates current `Active` / `Presented` RFQs as Open, regardless of creation date.
- Counts `ClosedHit` / `ClosedAway` events whose `OccurredAt` is within the selected date's
  desk-local `[00:00, next 00:00)` range converted to UTC.
- Keeps the existing `OutcomeCorrected` behavior and does not reinterpret it as a new close.
- Uses the existing event timestamp and adds no `ClosedAt` column.

## Verification coverage

- JST local-midnight boundaries and their corresponding UTC instants.
- Inclusion of the full `To` date and exclusion at the following local midnight.
- UTC dates that differ from the desk-local calendar date.
- Inclusion of older open RFQs in EOD.
- Hit/Away inclusion only on the selected local date, including both JST boundaries.
- Exclusion of prior-day outcomes and another desk's RFQs/events.
