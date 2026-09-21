# 08. Testing and Seed Data

## 1. Domain unit tests

Focus on typed state and transition invariants.

At minimum cover:

- initial Draft -> Active/Open confirm
- Active -> Presented -> Active
- Open -> Cancelled -> Active/Reopened
- Open -> Closed Hit/Away
- `QuoteRequested` / `QuoteConfirmed` construction and transitions
- invalid state combinations cannot be constructed through public APIs
- Quote Confirm coherently returns updated RFQ + immutable ConfirmedQuote
- Quote Confirm validates WorkingQuote/current Revision association
- Withdraw rejects Presented
- Expire from Active and Presented
- Amendment Save/Confirm/Discard
- old current Revision -> Superseded on amendment Confirm
- WorkingQuote transition behavior
- CaseMemo transitions
- ownership PickUp/Release/Assign/TakeOver
- `StateVersion` validation / checked Next
- representative Domain exception categories

Test Domain transitions as pure business operations without repository/clock dependencies.

---

## 2. Application/use-case tests

Use repository mocks/fakes where appropriate.

Focus on:

- correct Domain transition/factory invoked
- central authorization invoked
- IDs/time/business date allocated outside Domain
- initial Confirm creates WorkingQuote and persists atomically
- amendment Confirm creates/seeds a new WorkingQuote for the new Revision
- Quote Confirm allocates QuoteId outside Domain and persists both outputs
- calculation success revalidates state after external calculation
- calculation failure leaves WorkingQuote unchanged
- bulk partial-success behavior
- `CreateFromExisting` desk/business-date rule, including UTC/JST boundary regression

Application tests should use typed IDs/state rather than string status comparisons.

---

## 3. Infrastructure integration tests

Use **real PostgreSQL**, preferably Testcontainers.

Focus on:

- EF mappings/rehydration of typed lifecycle state
- migrations from the existing schema
- transaction atomicity
- one Draft partial unique index
- one WorkingQuote per Revision
- optimistic concurrency / `StateVersion` mapping
- Category/Security/Routing FKs
- JSONB event payloads
- quote-event joins after removal of `QuoteEvent.CaseId`
- expiry worker selection/transition behavior

Do not use EF InMemory/SQLite as a PostgreSQL substitute.

### Required event-cursor concurrency test

Prove the global event feed cannot lose an event when concurrent event-producing transactions are ordered/committed oppositely.

The test must exercise real PostgreSQL and the actual cursor-lock/allocation mechanism.

The invariant to prove is:

```text
once a client advances lastSeenEventId after committed events,
no event that later becomes visible may exist at a skipped lower cursor value
```

---

## 4. API tests

Cover representative:

- happy paths
- 400 validation
- 403 authorization
- 404
- 409 version conflict
- calculation failure mapping
- existing external DTO/JSON compatibility after internal typed refactor
- OpenAPI smoke test

---

## 5. FE tests

Existing FE test scope remains unchanged; backend semantic refactor should not require FE changes.

---

## 6. E2E journeys

Keep representative journeys:

1. Sales creates RFQ -> Trader quotes -> Sales presents -> Hit
2. Sales revises RFQ -> Trader requotes -> Away
3. Quote expires -> Requested/Expired -> requote
4. Cancel -> Reopen -> requote
5. WorkingQuote / Revision concurrency conflict

---

## 7. Master/seed strategy

Keep realistic but mock/seed master data.

### Category

Category is master data, not enum.

Seed a small set such as JGB / Corporate / Other with:

- stable CategoryId
- display Name
- CategoryRouting to default trader

Security rows reference CategoryId by FK.

### Other master data

Retain current pragmatic User/Desk/Client/Security seeds.

Security data should remain realistic enough to exercise search.

---

## 8. Demo RFQ seed

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
- multiple Contact Owners / Assigned Traders / ownership states
- Past RFQ search

Seed data must be constructible through the new valid Domain model or equivalent trusted persistence setup; do not seed impossible field combinations that Domain could not rehydrate.

---

## 9. Security seed from realistic Japanese bond data

Where practical, use public Japanese bond reference-price/security-list data as a realistic source for demo/search seed.

Useful extracted fields include:

- bond/security code
- name
- coupon
- maturity

Generate application `SecurityId` and add mock fields where needed:

- ISIN
- ticker / BBG-style display
- CategoryId

The goal is realistic search/demo behavior, not a legally authoritative security master.

---

## 10. Mock ISIN

For demo seed, structurally valid JP-prefixed ISIN-like identifiers with valid check digits may be generated where convenient.

They do not need to reproduce an issue's actual ISIN.

Important test properties:

- full 12-character lookup
- prefix lookup
- alphanumeric support
- checksum path if implemented

---

## 11. Calculation mock data

Do not build fake holiday, curve, convention, or yield-spread master systems inside the RFQ app.

Hide them behind MockCalculationClient.

The mock may use one deterministic formula across arbitrary securities and should produce plausible price/yield/simple-yield/spread/accrued/settlement/delta values plus controlled errors.
