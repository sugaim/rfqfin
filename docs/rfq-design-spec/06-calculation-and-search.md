# 06. Calculation and Search

## 1. Calculation service boundary

The RFQ application does not own detailed pricing conventions, curve resolution, or security convention logic.

Calculation receives typed business/application inputs per item, conceptually:

- SecurityId
- SettlementDate
- CalculationDriver
- typed CalculationParameter
- independent SimpleYieldSlide where applicable
- correlation/request ID

and returns one result per request.

```text
CalculateBulk(requests[]) -> results[]
```

Do not rely on array ordering alone.

External transport may map typed IDs to strings/Guids at the adapter boundary.

---

## 2. Bulk result semantics

One failed calculation does not fail the entire batch.

```text
CalculationResult =
    Success { payload }
  | Error { code, message }
```

---

## 3. Initial mock calculation client

Keep `ICalculationClient` with mock implementation.

Mock requirements:

- deterministic
- arbitrary Security IDs work
- plausible-looking values
- typed requests/results
- controllable per-item failure
- same broad bulk shape expected from future real service

Pricing accuracy is not the purpose of this application.

---

## 4. WorkingQuote update flow

Trader edits a quote cell.

1. load typed edit context including CaseId, RevisionId, SecurityId, ownership/state, `StateVersion`, and WorkingQuote
2. authorize actor centrally
3. verify expected Case/WorkingQuote versions
4. build typed calculation request
5. call calculation outside DB transaction
6. on Error:
   - do not change WorkingQuote
   - persist CalculationFailureLog
   - return failure so FE reverts attempted edit
7. on Success:
   - reload current RFQ state
   - revalidate current Revision, ownership/authorization, Case Version, WorkingQuote Version
   - call `WorkingQuoteTransitions.ApplyCalculated`
   - persist returned WorkingQuote

Do not compare statuses as strings such as `"Requested"`.

Do not hold DB locks during external/heavy calculation.

---

## 5. WorkingQuote factory

Creation is separate from update transitions.

Use a Domain factory that creates a WorkingQuote only for an eligible confirmed/current Revision.

Initial Confirm:

```text
RfqLifecycleTransitions.ConfirmInitial
-> WorkingQuoteFactory.CreateInitialFor(...)
-> persist atomically
```

Amendment Confirm:

- create a new WorkingQuote for the new current Revision
- supply seed WorkingQuote loaded by Application/Infrastructure when `QuoteSeedRevisionId` requires it
- Domain factory decides empty vs clone semantics from explicit inputs

Database uniqueness remains the final one-per-Revision guard.

---

## 6. Calculation failure log

Keep append-only failure logging with enough context to reproduce the attempted request:

- FailureLogId
- CaseId
- RevisionId
- TraderId
- RequestId
- driver/type/value
- slide
- prior WorkingQuote snapshot
- request/calculation context
- error code/message
- timestamp

Use typed Domain/Application IDs before persistence mapping.

---

## 7. Calculation context

Calculated ConfirmedQuotes preserve enough context for historical reproducibility, such as:

- market date/as-of
- snapshot tag
- reference securities/yields
- curve/context identifiers
- method-specific inputs

Use typed family-specific payloads; avoid a giant nullable field forest.

---

## 8. Standard settlement

Resolved by calculation/library boundary, not reimplemented in RFQ Domain.

```text
ResolveStandardSettlementDate(SecurityId, TradeDate)
    -> SettlementDate
```

On security change before confirm, recompute standard settlement.

Store standard and actual settlement in Revision terms.

Business date is resolved through the configured desk/business timezone abstraction, not server-local or UTC calendar date shortcuts.

---

## 9. Security and Category resolution

Security search belongs to the RFQ App Server boundary.

Conceptual abstraction:

```text
ISecuritySearch
- Search(query)
- Resolve(SecurityId)
```

Save canonical typed `SecurityId` in Application/Domain.

Security -> Category is master/DB data, not a hard-coded Domain enum rule.

The consolidated defaults resolver may coordinate:

```text
SecurityId
-> CategoryId
-> Default Assigned Trader
-> Standard Settlement Date
```

---

## 10. Security search behavior

Retain current search behavior and normalization strategies for:

- internal code
- BBG-like display/search
- ISIN prefix/full lookup

Union/deduplicate/rank results; exact/normalized exact matches rank above broad partials.

Do not hardcode JP-only numeric ISIN assumptions.

---

## 11. Client search

Simple autocomplete/partial search over available name/code fields.

Return canonical `ClientId`.

---

## 12. Search caching

Initial behavior remains direct PostgreSQL search/exact lookup.

Do not preload the full security master or aggressively cache arbitrary autocomplete queries.

If later needed, exact ID lookup is the first reasonable cache target.

---

## 13. Security search details

Retain the current canonical search rules.

Search strategies may run together; do not force every query through one exclusive parser branch. Union results, deduplicate, then rank. Exact/normalized exact/structured matches rank above prefix/partial matches.

### Internal code

Short form:

```text
{int}-{int}
```

Normalize conceptually to:

```text
0-02-XXXX-YYYYY
```

by trimming components and zero-padding latter fields.

Full form:

```text
{int}-{int}-{int}-{int}
```

Normalize widths approximately `[1, 2, 4, 5]` and support prefix/partial search.

### BBG-like search

Concept:

```text
Ticker Cpn [Mat] [#Series]
```

Rules:

- trim
- uppercase ticker
- collapse arbitrary whitespace
- coupon is decimal form
- maturity accepts `MM/DD/YY` and `MM/DD/YYYY`
- `#Series` optional
- ticker + coupon may search without maturity

### ISIN

After trim + uppercase, support prefix candidates.

Useful candidate pattern:

```text
^[A-Z]{2}[A-Z0-9]{5,10}$
```

- 7–11 characters -> prefix search
- 12 characters -> validate structure/checksum if desired

Do not hardcode JP-only numeric NSIN assumptions.

### Result display

Useful candidate columns:

- Japanese security name
- BBG-style display
- Internal Code
- ISIN

Issuer is optional if reliable issuer master data exists.

Limit results to top N and ask for more input when matches are broad.

---

## 14. RFQ defaults resolver

The FE may use a consolidated query after Security selection:

```text
ResolveRfqDefaults(SecurityId, TradeDate)
```

It may return:

- CategoryId / category display data
- Default Assigned Trader
- Standard Settlement Date

This keeps FE simple while Application coordinates master/routing/calculation lookups.
