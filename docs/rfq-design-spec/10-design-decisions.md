# 10. Design Decisions and Rationale

This document records non-obvious choices that should survive implementation handoff.

---

## 1. Domain state is immutable from callers

Business-state objects such as `RfqCase`, `RfqRevision`, `WorkingQuote`, and `CaseMemo` do not expose public mutation methods/setters.

State changes return new values through Domain transitions.

Rationale:

- makes transitions explicit and reviewable
- prevents Application code from bypassing business preconditions by setting fields directly
- reduces accidental partially-updated state
- matches the desired “data represents state; processing is outside the data” model

This does not require every type to be a record.

---

## 2. Typed lifecycle replaces field-combination source of truth

The earlier design kept Open state primarily as fields (`RfqStatus`, `QuoteStatus`, reason, current quote ID). The revised design uses:

```text
RfqLifecycle
├─ DraftRfq
├─ OpenRfq
│  ├─ ActiveRfq
│  │  ├─ QuoteRequested
│  │  └─ QuoteConfirmed
│  └─ PresentedRfq
├─ CancelledRfq
└─ ClosedRfq
```

Rationale:

- Presented always requires a quote and deserves its own RFQ state type
- Requested always requires a reason
- Confirmed/Quoted always requires a QuoteId
- impossible nullable/status combinations become unrepresentable through public construction

`RfqStatus` and `QuoteStatus` remain useful projection/transport concepts but are not a second mutable Domain truth.

---

## 3. Presented is an Open RFQ subtype

`PresentedRfq` is under `OpenRfq`, not a top-level lifecycle beside Open.

Rationale:

- the RFQ is still operational/open
- it retains Open-only concepts such as trader ownership
- Present/Unpresent changes customer-facing exposure while keeping the RFQ in the open workflow

---

## 4. Contact Owner and Assigned Trader are Case-level

They are not duplicated in every lifecycle subtype.

Rationale:

- they survive Cancel and Close
- they are Case responsibility/routing data rather than a property of Active/Presented alone
- duplication created synchronization risk

---

## 5. Trader ownership is typed and Open-only

Use `Ownership = Unowned | Owned` in `OpenRfq` rather than a Domain boolean `Owned`.

Rationale:

- `Owned` by itself was hard to read in UI/code context
- Domain can use explicit `Ownership` while UI displays “Picked Up”
- ownership has no operational meaning after leaving Open
- `Owned` still means owner == current `AssignedTraderId`; no second owner ID

Persistence may flatten to boolean.

---

## 6. Transitions are grouped by business meaning, not object count

Canonical examples:

```text
RfqLifecycleTransitions
RfqOwnershipTransitions
QuoteTransitions
AmendmentTransitions
WorkingQuoteTransitions
CaseMemoTransitions
```

Rationale:

- classification must remain stable if implementation later updates another related object
- “one state machine vs multiple objects” is an implementation-sensitive boundary and produced awkward categorization
- business vocabulary gives a more stable API

A transition may consume/return several Domain values.

---

## 7. Application is the Use Case layer

Do not add a separate UseCases project.

Application owns repository access, authorization, ID/time resolution, external services, event persistence, and transaction orchestration.

Domain transitions/factories own deterministic business-state rules.

Rationale:

- `Rfq.Application` already represents this layer
- splitting `Application` and `UseCases` would create an unclear project boundary with little dependency benefit
- Application size is managed by feature/use-case folders, not another assembly

---

## 8. Authorization and state validity are separate concerns

Domain checks whether the transition is valid for the current state.

Application authorization checks whether the current actor may perform it.

Rationale:

- role/desk/manager policy will evolve
- state invariants should not depend on current-user infrastructure
- central authorization avoids repeated predicates across handlers/controllers

---

## 9. Quote Confirm creates RFQ state + ConfirmedQuote coherently

Public Domain APIs must not allow Application to independently create a current ConfirmedQuote and separately mark the RFQ quoted.

Quote Confirm produces both:

```text
ActiveRfq(QuoteRequested)
+ WorkingQuote
->
ActiveRfq(QuoteConfirmed(new QuoteId))
+ immutable ConfirmedQuote
```

Rationale:

- otherwise Application can construct a ConfirmedQuote while RFQ stays Requested, or mark RFQ quoted without its corresponding snapshot
- this is a Domain invariant, not merely persistence orchestration

---

## 10. Quote identity allocation is outside the transition

Application allocates `QuoteId` and `RevisionId` and passes them into Domain APIs.

CaseId follows the existing DB/application sequence path.

Rationale:

- ID value generation is not a business transition rule
- deterministic transitions are easier to test
- local Guid creation does not justify an interface without a concrete replaceability requirement

---

## 11. QuoteConfirmation is a Domain concept

`ConfirmedBy + ConfirmedAt + expiry policy` belong together as confirmation metadata.

`QuoteId` remains separate because it is the identity of the new ConfirmedQuote.

Rationale:

- group values because they form a real concept, not because a method has “too many arguments”
- parameter count alone is not a reason to introduce wrapper objects

---

## 12. Expiry is typed, but future policies are not implemented early

Current Domain supports no expiry or positive fixed duration through a typed policy rather than raw `int?` minutes.

Rationale:

- future policies may not be expressible as N minutes
- raw nullable integer conflates absence, units, and policy
- current DB representation may remain minutes + resolved timestamp until requirements expand

---

## 13. WorkingQuote belongs to Revision and is immutable Domain data

A WorkingQuote is still conceptually one-per-Revision working state, but updates return a new WorkingQuote through `WorkingQuoteTransitions`.

Rationale:

- quote work is meaningful against a specific condition set
- historical revisions keep their own working values
- immutable Domain values make update/version semantics explicit

EF persistence may update the corresponding row in place.

---

## 14. WorkingQuote creation is a Domain factory

Initial Confirm does not need to make WorkingQuote creation part of the lifecycle transition itself.

Instead Application calls:

```text
ConfirmInitial transition
then WorkingQuote factory
then one atomic persistence commit
```

Rationale:

- “Open/Requested” is a coherent Domain state even before persistence orchestration creates its working object
- the final persisted use case still guarantees a WorkingQuote for the confirmed/current revision
- a Domain factory can validate which confirmed/current revision it is creating for without making repository queries

Amendment Confirm similarly creates a **new** WorkingQuote for the new Revision in the same Application transaction and may seed it explicitly.

---

## 15. CurrentRevision must be named for what it means

A property originally named `InitialRevision` must not later hold an amendment revision.

Use `CurrentRevision`.

Rationale:

- semantic naming matters more than historical creation order
- retaining the old name invites incorrect future logic

Transition results should carry affected old/new revisions when Application needs them; do not keep temporary mutable `PreviousRevision`/`DiscardedRevision` fields on the Case.

---

## 16. Revision is immutable and public low-level mutation is not exposed

Do not expose generic public `revision.Confirm()/Supersede()/Discard()` calls that Application can compose into an invalid Case.

Use business transitions (`InitialDraft`, `Amendment`, lifecycle confirm) and internal helpers as needed.

Rationale:

- Revision state is coupled to Case current-revision semantics
- low-level public operations would let callers violate Case invariants

---

## 17. StateVersion is a Domain value object over signed long

Use one common `StateVersion` for Case current state, Revision, WorkingQuote, and CaseMemo.

Requirements:

- signed long
- >= 1
- checked `Next()`

Rationale:

- PostgreSQL/EF/JSON naturally use signed long
- `ulong` creates boundary friction with little benefit
- one VO removes naked-version primitives without creating unnecessary per-entity version types

---

## 18. Category is master data, not enum

Category uses stable `CategoryId` plus mutable display `Name`.

Security and routing reference the ID through DB constraints.

Rationale:

- categories may change rarely but can legitimately be data-driven
- display-name changes must not break references/history
- security-to-category mapping is not currently a fixed code rule
- UI can avoid free-text mistakes by selecting from the master

---

## 19. Typed IDs/values are used inside Domain and Application

Application internals should not carry domain semantics as raw strings/Guids/longs where a Domain type exists.

Rationale:

- prevents accidental string status comparisons
- makes contexts/authorization signatures self-describing
- keeps primitive conversion at HTTP/DB/integration boundaries

Transport DTOs may remain primitive.

---

## 20. Restore/rehydration is not a public business API

Persistence may use internal rehydration APIs and `InternalsVisibleTo(Rfq.Infrastructure)`.

Rationale:

- public arbitrary Restore would let Application bypass transitions
- Infrastructure genuinely needs to reconstruct persisted state
- a persistence DTO does not by itself solve access control

---

## 21. Use a small Domain exception taxonomy

Use categories such as:

- DomainRuleViolationException
- DomainValidationException
- StateVersionMismatchException
- DomainInvariantException

Do not create exception subclasses for every operation.

Rationale:

- callers/API need to distinguish broad handling categories
- hundreds of micro-exceptions add maintenance without value
- invariant/data-corruption failures must not be mistaken for normal user validation

---

## 22. ConfirmedQuote is immutable

Presentation, withdrawal, and expiry are separate events/state transitions and never rewrite the confirmed snapshot.

Rationale remains historical reproducibility and clear separation between “what was confirmed” and “what happened later.”

---

## 23. Amendment is a Draft Revision, not an RFQ status

A pending amendment coexists with the current confirmed Revision and quote until amendment Confirm.

Rationale:

- editing a future condition is not the same dimension as current customer-facing/quote state
- avoids composite statuses such as AmendingQuoted/AmendingPresented

---

## 24. Reopen does not resurrect a confirmed quote

Reopen produces Active + QuoteRequested(Reopened) + Unowned.

WorkingQuote values remain available for review/reconfirmation.

Rationale:

- old communicated quote may no longer be valid
- explicit reconfirmation is required
- retaining working values avoids re-entry

---

## 25. Revision Confirm invalidates old current quote only on Confirm

Draft amendment editing does not invalidate current confirmed RFQ/quote.

On amendment Confirm, current quote/presentation is cleared and state becomes Requested(Revised).

Rationale:

- Draft edits are not official conditions
- confirmation is the atomic business boundary

---

## 26. Presentation is RFQ/customer-facing state

Presented is controlled by Contact Owner and protects the quote from trader withdrawal.

It does not prove literal customer receipt.

Rationale:

- primary meaning is customer-facing workflow/protection, not pricing state
- therefore Present/Unpresent belong to `RfqLifecycleTransitions`

---

## 27. QuoteEvent does not store CaseId

QuoteEvent stores QuoteId and derives Case through ConfirmedQuote -> Revision -> Case.

Rationale:

- storing both CaseId and QuoteId permits mismatched references unless a composite consistency constraint exists
- join cost is acceptable at current scale
- denormalization can be reconsidered only if demonstrated performance requires it

---

## 28. Event cursor order must follow commit visibility

`GetEventsAfter` must not use a cursor scheme that can skip a lower ID committed later.

The infrastructure must keep a commit-order-safe mechanism and prove it with a real PostgreSQL reverse-commit concurrency test.

Rationale:

- persisted events are reconnect recovery/source of truth for update notification
- silent event loss is unacceptable

---

## 29. Current operational state is stored directly, not rebuilt from events

CaseCurrent remains a flattened persistence projection/state table; events remain audit/notification/history.

Rationale:

- UI/search needs fast current state
- event sourcing complexity is not justified
- persistence flattening does not require flattening Domain state

---

## 30. Lightweight Command/Query separation remains

Commands load typed business state and run Domain transitions.

Queries may directly join/project DB state into view DTOs.

Rationale:

- history/search reads do not need aggregate reconstruction
- update paths still require Domain invariants and atomic transactions
- no full CQRS framework is needed

---

## 31. Do not auto-refresh active grids

Server changes mark updates available; user Refresh applies authoritative state.

Rationale remains avoiding invisible replacement of actively edited rows.

---

## 32. SSE is wake-up transport, not authoritative data

SSE signals change; persisted event query performs catch-up.

Rationale remains safe reconnect and transport replaceability.

---

## 33. Master/pricing realism remains intentionally behind boundaries

The RFQ application does not reimplement holiday/convention/curve/pricing systems.

Rationale remains keeping workflow validation separate from the firm's pricing stack.

---

## 34. `CreateFromExisting` uses business date, not UTC calendar date

The “source Case created today” rule must compare desk/business dates.

Rationale:

- a JST desk can be on a different calendar date than UTC
- using `CreatedAt.UtcDateTime.Date` gives incorrect settlement-copy behavior near midnight/business-date boundaries

---

## 35. Keep two client-side change windows

The Changes UI keeps `Last Refresh` and `Pending Updates` rather than erasing all change information on Refresh.

Rationale:

- users can see what the most recent refresh incorporated
- two generations are sufficient operationally
- long-term audit remains persisted in Event tables

---

## 36. Separate update indication from important toasts

Most cross-session changes only mark Updates Available. Important transitions may additionally produce an in-app toast.

Rationale:

- desk-wide event volume can be high
- toast storms are worse than explicit refresh
- Close/Withdraw/Expiry/TakeOver-type changes may deserve immediate attention

---

## 37. Keep initial Past RFQ search simple

Use server query + indexes + approximately 20k-row cap and client-side grid sort/filter initially.

Rationale:

- expected data scale is manageable for PostgreSQL
- actual business query patterns are not yet fully known
- premature paging/materialized-view infrastructure would lock in assumptions

---

## 38. Persist all cross-session observable transitions

Any business transition another active session must discover through Pending Updates must append a persisted event in the same transaction as state mutation.

At minimum this includes quote confirmation as well as Revision Confirm, lifecycle transitions, ownership/responsibility transitions where notification is required, and Presentation/Withdraw/Expire.

Rationale:

- SSE is only a wake-up channel
- reconnect recovery depends on the persisted feed
- event coverage is determined by cross-session observability, not whether an event feels like audit history
