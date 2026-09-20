# 06. Calculation and Search

## 1. Calculation service boundary

The RFQ application should not own security convention, curve resolution, or detailed pricing logic.

The calculation service boundary receives, per item:

- Security ID
- Settlement Date
- Calculation Type
- typed Calculation Parameters

and returns a result per request.

Conceptually:

```text
CalculateBulk(requests[]) -> results[]
```

Each request has a correlation/request ID.

Do not rely on array ordering alone.

---

## 2. Bulk result semantics

One failed calculation does not fail the entire batch.

Example:

```text
Request A -> Success
Request B -> Error
Request C -> Success
```

Result is therefore logically:

```text
CalculationResult =
    Success { ...outputs... }
  | Error { code, message }
```

This supports multi-row trader calculation and bulk workflows.

---

## 3. Initial implementation: mock calculation client

Use an interface such as:

```text
ICalculationClient
```

with:

```text
MockCalculationClient
RealCalculationClient   // future
```

Initial mock requirements:

- deterministic
- arbitrary Security IDs work
- same broad logic for all securities is acceptable
- returns plausible-looking values
- supports typed calculation requests
- can intentionally return per-item failures for testing
- preserves the real bulk request/response shape

The initial goal is UI/application-flow validation, not pricing accuracy.

---

## 4. WorkingQuote update flow

Trader edits a quote cell.

1. determine driver from edited column
2. read current WorkingQuote and expected version
3. build calculation request
4. call bulk calculation interface
5. on Success:
   - commit WorkingQuote/result
   - increment WorkingQuote version
6. on Error:
   - do not update WorkingQuote
   - FE reverts attempted cell
   - show failure toast
   - persist CalculationFailureLog

Prefer calculating before a short DB write transaction.

Do not hold DB locks during an external/heavy calculation.

Use optimistic version check when writing result.

---

## 5. Calculation failure log

Initial design logs failures, not every successful calculation attempt.

Suggested data:

- FailureLogId
- CaseId
- RevisionId
- TraderId
- attempted driver/type/value
- prior WorkingQuote
- calculation/market context
- request payload or reproducible request snapshot/reference
- error code
- error message
- timestamp

The API returns the failure log ID to the UI where useful.

A future requirement for complete calculation auditing can generalize this into `CalculationAttempt`.

---

## 6. Calculation context

Calculated ConfirmedQuotes must preserve enough context for historical reproducibility.

Examples:

- market date / as-of
- snapshot tag
- reference security IDs
- relevant reference yields
- curve/context identifiers
- method-specific inputs

Avoid a giant nullable column forest in the domain model.

Use typed family-specific context payloads.

---

## 7. Standard settlement

Standard settlement is resolved by the calculation library/service, not reimplemented in the RFQ application.

Conceptually:

```text
ResolveStandardSettlementDate(
    SecurityId,
    TradeDate
) -> SettlementDate
```

On security change before confirm, recompute the standard settlement.

Store the resolved standard alongside the actual settlement in the Revision.

Initial mock returns a deterministic plausible date.

---

## 8. Security search API

Security search belongs to the RFQ App Server API boundary, not directly to the calculation server.

Conceptual internal abstraction:

```text
ISecuritySearch
- Search(query)
- Resolve(id)
```

Initial implementation may query local/mock master data.

Future implementation may delegate to another service without changing the FE contract.

Save canonical `SecurityId` on the RFQ.

---

## 9. Security search behavior

One input field supports several search strategies.

Search strategies may run together; do not force every query through one exclusive parser branch.

Union results, deduplicate, then rank.

Exact/normalized exact/structured matches rank above prefix/partial matches.

### Internal code

Short form:

```text
{int}-{int}
```

Normalize conceptually to:

```text
0-02-XXXX-YYYYY
```

Trimming components and zero-padding the latter fields.

Full form:

```text
{int}-{int}-{int}-{int}
```

Normalize widths approximately:

```text
[1, 2, 4, 5]
```

Prefix/partial search is supported.

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
- ticker + coupon may search even without maturity

### ISIN

After trim + uppercase, support prefix candidates.

Useful search candidate:

```text
^[A-Z]{2}[A-Z0-9]{5,10}$
```

- 7–11 characters -> prefix search
- 12 characters -> validate structure/checksum if desired

Do not hardcode JP-only numeric assumptions; ISIN NSIN content can be alphanumeric.

### Result display

Useful candidate columns:

- Japanese security name
- BBG-style display
- Internal Code
- ISIN

Issuer is optional if reliable issuer master data exists.

Limit results to top N and ask for more input when matches are broad.

---

## 10. Client search

Simpler autocomplete/partial search.

Support as available:

- Japanese/English name
- internal client code

Return canonical `ClientId`.

No need for complex parser logic.

---

## 11. Search caching

Initial implementation:

```text
Search -> PostgreSQL
Exact ID lookup -> PostgreSQL
```

Do not preload the entire security master into application memory.

Do not aggressively cache autocomplete query strings.

If performance later requires caching, the first good targets are exact lookups:

```text
SecurityId -> Security
ClientId -> Client
```

rather than arbitrary prefix-query result sets.

---

## 12. RFQ defaults resolver

The FE benefits from a consolidated query after Security selection.

Conceptually:

```text
ResolveRfqDefaults(SecurityId, TradeDate)
```

May return:

- Category
- Default Assigned Trader
- Standard Settlement Date

This keeps the FE simple while allowing the application server to coordinate routing/master/calculation-service lookups.
