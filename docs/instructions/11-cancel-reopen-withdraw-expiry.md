# Step 11 — Cancel, Reopen, Withdraw, and Expiry

## Goal

Complete the remaining major lifecycle transitions and automatic quote expiry.

## Read first

- Cancel/Reopen/Withdraw/Expiry sections in canonical design
- `02-state-transitions.md`
- `07-runtime-and-notifications.md`
- Design Decisions 8, 9, and 12

## Implement

### Withdraw

Trader operation.

Allowed when:

- QuoteStatus = Quoted
- RFQ is not Presented

Effects:

- RfqStatus = Active
- QuoteStatus = Requested
- QuoteRequestReason = Withdrawn
- current quote association removed
- WorkingQuote retained

Support multi-select command semantics; Presented rows are skipped/reported, not silently mutated.

### Cancel

Open -> Cancelled.

Retain:

- Assigned Trader
- Contact Owner
- WorkingQuote
- ConfirmedQuote history
- Draft amendment may remain Draft

Set Owned=false.

### Reopen

Cancelled -> Open:

```text
RfqStatus = Active
QuoteStatus = Requested
QuoteRequestReason = Reopened
Owned = false
```

Do not resurrect old ConfirmedQuote as current.

Retain WorkingQuote values.

### Expiry

Implement ASP.NET Core `BackgroundService`.

Initial assumption:

- one App Server instance
- approximately 10-second interval

Find currently quoted rows with `ExpiresAt <= now`.

Call the normal Application `ExpireQuote` use case.

Expiration must be:

- idempotent
- concurrency-safe

Effects:

- Presented -> Active if needed
- Quoted -> Requested
- reason -> Expired
- current quote association removed
- WorkingQuote retained

Do not implement leader election/distributed worker logic.

## Completion criteria

Manual:

- Withdraw ordinary quote
- verify Presented quote cannot Withdraw
- Cancel + Reopen preserves working values but requests quote again
- Confirm a quote with 1-minute expiry and observe automatic expiration

For faster automated/manual development tests, a configurable shorter expiry/worker interval is acceptable only in Development configuration.

## Tests

- all transition preconditions
- idempotent expiry
- concurrent expiry/manual operation
- Reopen does not restore current quote
- Withdraw Presented rejection

## Commit boundary

```text
add cancel reopen withdraw and expiry
```
