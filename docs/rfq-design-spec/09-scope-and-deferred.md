# 09. Scope, Non-goals, and Deferred Work

## 1. Explicitly out of initial scope

### External inbound channels

No Bloomberg Chat/direct inbound adapter initially.

Keep source extensible, but Manual creation is enough.

### New Bulk / List / Thread / Portfolio workflow

Do not design generic grouping before desk workflow is known.

Core Case design must allow future external grouping without adding parent-case semantics.

### Real calculation server

Initial integration uses MockCalculationClient.

The interface must match the intended bulk request/result behavior.

### Realtime market feed

No realtime market data integration initially.

Calculated quote flow uses mocked/close-based context and manual simple-yield slide.

### Full authentication architecture

Current user/roles can be resolved by the hosting environment.

Transport/mechanism is not specified here.

### Multi-instance App Server

Single instance initially.

No Redis, distributed event fan-out, or leader election.

### Full event sourcing

Events are audit/notification/history.

Current state is stored directly in CaseCurrent.

### Sophisticated Past RFQ paging

Initial cap is approximately 20k returned rows.

Introduce keyset/infinite paging only if real usage needs it.

### Materialized search model

Do not create a large precomputed Past RFQ snapshot until query performance proves necessary.

### Desktop/browser notifications

Initial important notifications are in-app toasts.

Desktop Notifications API can be added later.

### Manager workflow

Manager role can be represented and authorization centralized, but manager-specific overrides are deferred.

### Pricer apply-back

Pricer is independent scratch state initially.

Official WorkingQuote apply-back is deferred.

---

## 2. Deliberately simplified initial assumptions

- Side = Customer Sell
- Security Type = Bond
- Contact Owner exists
- Assigned Trader exists by Confirm
- one Draft Revision maximum per Case
- one WorkingQuote maximum per Revision
- Quote expiry policy = None or N minutes
- expiry check interval ≈ 10 seconds
- no auto-Away at EOD
- no automatic UI refresh of main RFQ data
- security/client search hits DB directly without broad query-result caching

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
- full browser/desktop notifications
- paging / large-history optimization
- dedicated search projection
- event archival/partitioning
- richer expiry policies such as AM/PM/EOD
- complete calculation attempt audit
- pricer -> WorkingQuote apply-back
- external manager/master services

---

## 4. Known implementation-sensitive areas

These should be validated during implementation rather than over-specified now:

- exact EF Core mapping shape for lifecycle-specific nullable columns
- exact serialization schema for WorkingQuote/CalculationContext payloads
- endpoint URL naming
- OpenAPI DTO naming
- SSE behavior through the real reverse proxy/load balancer
- exact existing-user authentication integration
- practical Security search index strategy
- ag-Grid keyboard shortcuts
- exact Sales screen lower-panel contents
- actual EOD desk operating procedure
- manager override policy

The design intentionally leaves these adaptable.
