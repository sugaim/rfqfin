# Step 13 — Past Search, EOD, Pricer, and Grid Configuration

## Goal

Add the secondary operational surfaces after the primary RFQ lifecycle works.

## Read first

- Past RFQ / EOD / Pricer / Grid config sections in `04-ui-ux.md`
- search notes in `06-calculation-and-search.md`

## Implement

### Past RFQ search

Trader lower panel defaults to Past RFQ.

One Case = one row.

Server-side filters:

- date range
- Client
- Security
- Category
- Contact Owner
- Sales
- Assigned Trader
- outcome/status
- quote state if useful
- CaseId

Return up to approximately 20k rows.

If result exceeds cap, return a clear "narrow search" response.

Frontend uses AG Grid client-side sort/filter on returned rows.

Do not implement infinite scroll yet.

### Revision / Quote history

Implement the canonical query-side history surfaces:

- `GetRevisionHistory(CaseId)`
- `GetQuoteHistory(CaseId)`

These are read-model queries and must not reconstruct the full command aggregate. Show immutable/superseded/discarded Revision history and ConfirmedQuote history needed for operational investigation.

### EOD

Separate tab.

Summary:

```text
Contact Owner | Open | Hit | Away
```

Open count drills into unclosed RFQs for that owner.

Do not grant desk-wide Hit/Away authority merely because data is visible here.

### Pricer

Independent scratch state in right-side drawer.

Works:

- from selected RFQ: copy values into scratch state
- without selection: arbitrary Security/Notional
- uses mock CalculationClient-backed API
- not auto-synchronized after opening

No Apply-back to RFQ.

### Grid config

Persist:

```text
UserGridConfig
- UserId
- ScreenId
- ConfigKey
- Version
- ConfigJson
- UpdatedAt
```

Store:

- visibility
- order
- width
- pinning
- optionally sort/filter

Add an application-controlled config migration/version hook so column ID changes can be handled later.

## Completion criteria

- Past search is useful against seeded history
- Revision and Quote history are inspectable for a selected Case
- >20k query produces narrowing message
- EOD counts agree with DB
- scratch Pricer works without RFQ
- grid layout survives browser restart because it is server-stored

## Tests

Query integration tests with PostgreSQL, including Revision/Quote history ordering and content.

Grid config roundtrip/version test.

Pricer independent-state frontend test.

## Commit boundary

```text
add history eod pricer and grid preferences
```
