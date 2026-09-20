# 08. Testing and Seed Data

## 1. Test strategy

Use different test styles for different risks.

### Domain unit tests

Focus on state-machine/business invariants:

- Draft -> Open
- Open -> Cancelled -> Open
- Open -> Closed
- Present / Unpresent
- Requested / Quoted
- Revision Confirm
- Withdraw / Expire
- invalid lifecycle transitions
- Quote request reasons

### Application/use-case tests

Use repository mocks/fakes.

Focus on:

- correct domain operation invoked
- correct authorization checks
- correct related objects updated
- expected errors returned
- bulk partial success behavior
- EnsureWorkingQuote behavior

### Infrastructure integration tests

Use **real PostgreSQL**, ideally via Testcontainers.

Focus on:

- EF mapping
- migration
- transaction atomicity
- unique/partial indexes
- optimistic concurrency
- JSONB Event serialization/deserialization
- queries
- expiry worker selection/transition behavior

Do not rely on EF InMemory provider as a substitute for PostgreSQL behavior.

### API tests

Cover representative:

- happy paths
- validation
- 403
- 404
- 409 conflict
- CalculationFailure mapping
- OpenAPI generation smoke test

### FE tests

Focus on important behavior, not every component:

- grid edit -> expected API
- calculation failure reverts cell
- conflict reload behavior
- Refresh discards FE-only edits
- Pending / Last Refresh change windows
- permission-based disable/availability
- Grid config restore

### E2E

Keep a small number of representative business journeys:

1. Sales creates RFQ -> Trader quotes -> Sales presents -> Hit
2. Sales revises RFQ -> Trader requotes -> Away
3. Quote expires -> Requested/Expired -> requote
4. Cancel -> Reopen -> requote
5. WorkingQuote conflict / Revision conflict

---

## 2. Mock/master strategy

Initial implementation uses mock/seed master data.

Prepare only what the app needs.

### User

Fields:

- UserId
- Name
- roles
- Desk

Include several Sales, Traders, and optionally Manager-shaped records.

### Desk

A few fixed desks are sufficient.

### Category

A small realistic set is sufficient, e.g. JGB / Corporate / Other or desk-appropriate categories.

### Routing

Simple:

```text
Category -> Default Trader
```

### Client

Tens of fictional/seed clients are sufficient.

### Security

Use realistic data because security-search behavior matters.

---

## 3. Security seed from JSDA reference-price data

Use the publicly available Japanese bond reference-price/security list data as a convenient realistic source where practical.

Extract useful fields such as:

- bond/security code
- name
- coupon
- maturity

Generate application `SecurityId`.

Add mock fields as needed:

- ISIN
- ticker / BBG-style display
- category

The goal is realistic search/demo data, not a legally authoritative security master.

---

## 4. Mock ISIN

For seed/demo purposes, generate structurally valid JP-prefixed ISIN-like identifiers with valid check digits where convenient.

They do not need to reproduce the real issue's actual ISIN.

The important test properties are:

- full 12-character lookup
- prefix lookup
- alphanumeric support
- checksum path if implemented

---

## 5. Calculation mock data

Do not build fake holiday, curve, convention, or yield-spread master systems inside the RFQ app.

Hide those behind the mock CalculationClient.

The mock may use one deterministic formula across arbitrary securities.

It should produce plausible:

- price
- yield
- simple yield
- spread
- accrued
- settlement amount
- delta

and support controlled errors.

---

## 6. Demo RFQ seed

Generate enough RFQs to exercise:

- Draft
- Requested
- Quoted
- Presented
- Cancelled
- Hit
- Away
- Revised history
- Expired/Withdrawn events
- multiple Contact Owners
- multiple Assigned Traders
- Past RFQ search

Hundreds to a few thousand synthetic historical Cases are enough for early UI/search testing.

Performance testing can generate much larger volumes separately.
