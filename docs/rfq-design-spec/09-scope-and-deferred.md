# 09. Scope, Non-goals, and Deferred Work

## 1. Explicitly out of initial/current scope

### External inbound channels

No Bloomberg Chat/direct inbound adapter initially.

### New Bulk / List / Thread / Portfolio workflow

Do not design generic grouping before desk workflow is known.

### Real calculation server

Current integration uses MockCalculationClient; preserve replaceable typed boundary.

### Realtime market feed

No realtime market-data integration initially.

### Full authentication architecture

Current user/roles are supplied by hosting environment; transport is not specified here.

### Multi-instance App Server

Single instance initially; no Redis/distributed fan-out/leader election.

### Full event sourcing

Events are audit/notification/history. Current state is stored directly.

### Sophisticated Past RFQ paging / large materialized search model

Keep the initial pragmatic query/index/cap design until real usage demands more.

### Desktop/browser notifications

In-app notifications only initially.

### Manager workflow

Authorization is centralized so Manager overrides can be added later; policy is deferred.

### Pricer apply-back

Pricer remains independent scratch state initially.

### Dynamic category administration UI

Category is master data and routing is configurable in DB, but a new admin UI/workflow is not part of the current scope.

---

## 2. Deliberately simplified current assumptions

- Side = Customer Sell
- Security Type = Bond
- Contact Owner exists
- Assigned Trader exists by Confirm
- one Draft Revision maximum per Case
- one WorkingQuote maximum per Revision
- quote expiry supports None or a positive fixed duration
- expiry check interval ≈ 10 seconds
- no auto-Away at EOD
- no automatic UI refresh of main RFQ data
- security/client search hits DB directly without broad result caching
- Category values come from master data rather than a compile-time enum

---

## 3. Future extensions the design should tolerate

- List / Thread / Bulk creation
- external RFQ adapters
- real calculation server
- realtime market snapshots
- multiple active quote variants
- richer quote lifecycle/read model
- Manager/admin overrides
- multiple application instances
- distributed notification transport
- browser/desktop notifications
- paging / large-history optimization
- dedicated search projection
- event archival/partitioning
- richer expiry policies such as fixed time / AM / PM / EOD
- complete calculation attempt audit
- pricer -> WorkingQuote apply-back
- external manager/master services
- richer category/security mapping rules

---

## 4. Known implementation-sensitive areas

Keep adaptable:

- exact EF Core flattened mapping of typed lifecycle states
- exact serialization schema for WorkingQuote/CalculationContext payloads
- endpoint/DTO naming
- SSE behavior through real proxy/LB
- existing-user auth integration
- security-search index strategy
- grid keyboard shortcuts
- Sales lower-panel details
- actual EOD procedure
- manager override policy

The design intentionally fixes business invariants while leaving these implementation details replaceable.
