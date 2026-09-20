# Step 04 — Client/Security Search and RFQ Defaults

## Goal

Replace raw identifier entry with usable Client/Security search and deterministic RFQ defaults **before Initial Confirm is introduced**.

## Read first

- canonical Security/Client Search in `06-calculation-and-search.md`
- standard settlement rules
- seed/master section in `08-testing-and-seed.md`

## Implement

### Master persistence

Add simple development master models/tables:

- User
- Desk
- Category
- CategoryRouting
- Client
- Security

This is mock/internal master data, not an authoritative firm-wide master system.

### Security search

Introduce `ISecuritySearch`.

Support the canonical input strategies:

- internal code normalization/search
- BBG-like text search
- ISIN/prefix search

Union/dedupe/rank results.

Return candidate fields useful to UI:

- SecurityId
- Japanese name
- BBG-like display
- Internal Code
- ISIN
- Category

### Client search

Simple code/name search -> ClientId.

### RFQ defaults

Implement:

```text
ResolveRfqDefaults(SecurityId, TradeDate)
```

returning at least:

- Category
- default Assigned Trader
- StandardSettlementDate

Also provide enough User master data for the Sales create form to resolve/display Contact Owner and Assigned Trader. For a Sales-created RFQ, default `ContactOwnerId` to the current user; Sales may override Assigned Trader before Confirm.

For now StandardSettlementDate may be deterministic mock logic hidden behind the calculation/default boundary.

### Frontend

Use Ant Design search/autocomplete behavior for Client/Security.

On Security selection:

- resolve defaults
- populate Standard Settlement
- default actual Settlement to Standard Settlement
- populate Category/Assigned Trader display as appropriate

Do not build a global application cache.

## Completion criteria

A user can create and save a Draft RFQ without knowing internal GUID/ID values, and all values required by the later Initial Confirm flow can be resolved/defaulted.

Search examples for internal code, ticker-like form, and ISIN prefix return sensible deterministic results.

## Tests

- normalization tests for each search grammar
- duplicate/ranking behavior
- default routing resolution
- API tests for search endpoints
- frontend search selection populates fields

## Commit boundary

```text
add master search and rfq defaults
```
