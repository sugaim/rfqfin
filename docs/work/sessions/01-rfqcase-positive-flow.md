# Design Session 01 — RfqCase Positive Flow

## Goal

Redesign the positive-flow RfqCase Domain model from business meaning rather than from the structure of the existing implementation.

The central problem was to obtain one coherent model for the internal Bond RFQ workflow between Contact Owner and Quote Owner that could represent:

- initial pricing;
- firm quote creation and replacement;
- customer presentation;
- Hit and Away;
- continued negotiation after an Away;
- repricing and ownership/date changes;
- normal Case closure without a Presentation;
- cancellation as a distinct non-normal outcome;

without carrying forward stale Draft/Revision/Requested/Confirmed abstractions merely because the implementation already contained them.

The session deliberately focused on **positive RfqCase flow first**. Draft lifecycle, correction/reversal, and detailed Application authorization were kept outside the design unit so that exception and pre-publication concerns would not distort the normal Case model.

This file is a completion record, not canonical Domain authority. See `../../domain.md` for the current model.

## Completed Domain design

### RfqCase boundary and identity

RfqCase was established as an Aggregate Root representing the formal RFQ after the Case has entered the pricing workflow.

Case-local child identity was adopted for the principal child entities. The important consequence was that historical Terms, Episodes, Quotes, and Presentations could retain stable identity without requiring every child ID to be globally meaningful.

Draft and unassigned/requested workflow were removed from RfqCase.State. The positive-flow Case begins only when enough responsibility exists to create a valid pricing context.

The later RfqDraft design made the pre-publication side explicit as a separate Aggregate Root; that later design confirms rather than reverses this RfqCase boundary.

### Current Terms and immutable history

RfqTerms became an immutable Case-local child Entity rather than a mutable bag of Case fields or a complete snapshot embedded independently into each pricing round.

The Case keeps the current RfqTerms. PricingEpisode refers to the RfqTermsId under which that pricing round was created, allowing historical Episodes to retain their original Terms identity.

Within the same Case, the positive-flow design allowed only the Terms changes that still represented the same customer inquiry before negotiation had begun. Material Terms changes after customer presentation were intentionally pushed toward creation of a new Case rather than mutation of the negotiated Case.

This was an important boundary handed to the later Draft/Copy discussion.

### PricingEpisode as the pricing-round concept

PricingEpisode was introduced/refined as the immutable Domain concept representing one logical pricing round.

The important meaning was not "one quote" and not "one screen snapshot." An Episode identifies the pricing context and why a new round exists.

Episode creation was used for semantically meaningful resets such as:

- Terms change;
- Quote Owner change;
- pricing-date/context change;
- a request for repricing;
- continuing after a Presentation became Away.

PricingEpisodeOrigin records the reason for the new round without duplicating the data already stored on the resulting/current entities.

A key consequence is that Application composition may legitimately create an intermediate Episode with no Quote. Episode count is therefore not equivalent to quote count.

The later RfqDraft session refined the pricing context further by separating AssumedTradeDate from PricingDate and by changing settlement-lag semantics accordingly. Those later refinements are current canonical truth and should not be reconstructed from the earlier intermediate date model.

### Quote, FirmQuote, and QuotePresentation are different business facts

The session separated three concepts that had previously been easy to conflate:

- Quote: a price condition produced in a PricingEpisode;
- FirmQuote: the current firm status of a Quote, including ValidUntil;
- QuotePresentation: the business event that a Quote was actually proposed to the customer.

This separation was central to the final model.

It allows, without inventing artificial states:

- a Quote that is never presented;
- replacement of an unpresented FirmQuote;
- a Presented Quote to stop being current when another firm price replaces it;
- the customer-facing Presentation to remain a historical business fact after the current FirmQuote changes;
- Hit/Away to attach to the customer proposal rather than to the bare price value.

QuotePresentation was briefly questioned as an independent Entity during the design exploration, but it was restored once Presentation had its own business identity/date and became the correct target of customer outcome.

No separate OutcomeId was required: the Presentation identifies the effective outcome.

### PresentationOutcome and CaseOutcome are different levels

The session distinguished:

- PresentationOutcome: what happened to a particular customer Presentation;
- CaseOutcome: how the RFQ Case finally ended.

This distinction was required because a Presentation may go Away while the Case continues into another PricingEpisode.

PresentationOutcome was therefore modeled as Hit or Away, while Case closure could reuse the latest Presentation Away outcome or close without any Presentation at all.

A Case that never reached a customer Presentation can close as an unpresented outcome rather than manufacturing a Presentation or Quote outcome that never occurred.

Cancellation was kept distinct from an ordinary valid RFQ outcome. Exact CancellationReason taxonomy was deliberately deferred.

### State model

The positive-flow state model converged on:

    Open
    ├─ Inquiry
    │  ├─ Pricing
    │  └─ PendingPresentation
    └─ Negotiating
       ├─ Pricing
       ├─ PendingPresentation
       └─ Presented

plus terminal Closed/Cancelled states.

The phase distinction has business meaning:

- Inquiry means no customer Presentation has yet occurred;
- Negotiating means at least one Presentation has occurred.

The work-state distinction also has business meaning:

- Pricing means there is no current FirmQuote;
- PendingPresentation means a current FirmQuote exists but has not been presented;
- Presented means the current FirmQuote is represented by the current customer Presentation.

The session explicitly rejected a generic orthogonal ClientState × QuoteWorkState model. Only combinations with real invariant differences were retained as Domain states.

### WorkingQuote remains outside Domain

WorkingQuote/repricing work-in-progress was deliberately kept in Application/UI state.

This solved an important real workflow without expanding the Domain state machine: a trader may prepare another price while the existing Presented FirmQuote remains live and hittable. Domain remains Presented until a business fact changes.

When the new price becomes firm, ReplaceFirmQuote changes the Domain state. Before that point, tentative pricing work does not.

This was also why a dedicated Domain Repricing state was rejected.

### LatestPresentation is current business state, not a full history collection

Negotiating states that are no longer currently Presented retain the latest QuotePresentation and, when applicable, its Away outcome.

This was needed so normal Domain operations such as CloseAway can determine whether the latest customer proposal already has an Away outcome without requiring the aggregate to load its complete Presentation history.

The design therefore drew a deliberate line:

- full historical collections may live in persistence/read models;
- the aggregate carries only historical facts that are still necessary to determine current valid behavior.

This is the rationale behind LatestPresentation / LatestPresentationAwayOutcome. It should not be generalized into "load all history into the aggregate."

## Important operation semantics

The session completed the positive-flow transition set sufficiently to walk every Open state through normal pricing, presentation, Hit/Away, and closure paths.

Several operation distinctions were intentionally preserved even when source/target state shapes looked similar.

### ReplaceFirmQuote is not InvalidateQuote + CommitQuote

Replacement is one business action. It does not imply that a separate withdrawal or customer rejection occurred.

When replacing a Presented FirmQuote, the old Presentation remains a valid historical/latest customer proposal, while the new FirmQuote becomes pending presentation.

### InvalidateQuote and ExpireQuote remain distinct

Both may remove the current FirmQuote, but the business meanings differ:

- InvalidateQuote is an explicit business withdrawal;
- ExpireQuote reflects quote-validity semantics.

They therefore remain distinct Domain operations.

### RequestRepricing is not InvalidateQuote

RequestRepricing starts another logical pricing round and therefore creates a new PricingEpisode.

A simple invalidation removes the current FirmQuote without claiming that a new pricing round was requested.

### ContinueAfterAway is not ordinary repricing

ContinueAfterAway first establishes an Away result for the current Presentation and then starts another pricing round whose origin refers to that outcome.

This preserves the fact that the customer proposal actually went Away while allowing the Case to remain open.

### Replace/Invalidate/Expire do not fabricate Away

Leaving Presented because the firm price was replaced, explicitly withdrawn, or expired does not by itself mean the customer rejected the Presentation.

The Presentation may remain without an outcome. If the Case later closes Away and that Presentation is still the latest relevant Presentation, CloseAway can create the Away outcome at that time.

### FirmQuote validity is explicit Domain state

ValidUntil belongs to the current FirmQuote rather than to Quote identity.

Extending validity keeps the same Quote identity while the quote is still valid. An already-expired quote is not revived by extension.

Clock passage alone does not mutate the aggregate; expiry is applied through an explicit Domain operation, while Hit validity must still reject acceptance after ValidUntil.

## Domain / Application boundary decisions

A major part of the session was not just choosing types, but deciding what **does not** belong to RfqCase Domain operations.

### Authorization does not define Domain operation identity

Current user, role, desk, and authorization policy belong to Application.

Different human workflows do not require different Domain operations when they have the same Domain effect.

For example, takeover, ordinary handoff, or management reassignment may have different authorization/orchestration but can ultimately use the same ownership-change Domain operation if the resulting business state is identical.

Conversely, operations with different business meaning remain distinct even when authorization is identical and state shapes happen to match.

This became a general design rule:

> split Domain operations by business meaning/invariants, not merely by actor or UI action.

### Application owns orchestration and external context

Application is responsible for concerns such as:

- resolving current user and authorization;
- supplying IDs;
- resolving BusinessEntity-local date/calendar context;
- external pricing/calculation;
- persistence and transaction boundaries;
- audit actor/timestamp;
- composing multiple Domain operations when one user use case requires them.

Domain does not read a clock, repository, current-user service, or external calendar directly.

### Local date and absolute time are different concepts

The session replaced the earlier overly strong "business day" interpretation with BusinessEntityLocalDate: a local calendar date in the BusinessEntity context that does not itself guarantee that the entity is open on that date.

Whether a supplied date is an allowed operating day belongs to Application/external calendar policy.

Absolute validity facts such as FirmQuote.ValidUntil and HitAt use Timepoint rather than the local-date concept.

The later RfqDraft session further refined the relation among PricingDate, AssumedTradeDate, PresentationDate, and HitDate. Current `domain.md` is authoritative for those exact date invariants.

## Persistence boundary decisions

The Domain design requires historical identity but does not require event sourcing or loading all historical entities into the Aggregate Root.

Important consequences established in this unit:

- previous RfqTerms/PricingEpisodes/Quotes/Presentations/outcomes must not be silently destroyed by a current-state update;
- current RfqCase need only carry historical information required by current Domain behavior;
- persistence/read models may retain and expose the full history independently of aggregate loading shape;
- Domain objects should not acquire audit timestamps, actor IDs, or persistence-specific fields merely to make storage convenient;
- Application/Persistence owns atomic persistence of composed use cases and optimistic-concurrency mechanics.

The exact relational schema and history representation were not part of this positive-flow design unit.

## Important refinements and rejected alternatives

The design did not arrive at the final shape in one pass. Several intermediate ideas were deliberately removed or refined.

### Draft / Requested / Unassigned inside RfqCase

Rejected.

Pre-publication or not-yet-routable work should not weaken the invariants of a formal RfqCase. This became the boundary for the next RfqDraft design unit.

### Revision as the central RFQ versioning concept

Not retained as the positive-flow Domain model.

Business changes were instead represented using concrete immutable entities and operations: RfqTerms, PricingEpisode, Quote, QuotePresentation, and their identities.

The current implementation may still contain Revision-era concepts; they are migration input, not evidence that the Domain should return to that model.

### Notes inside Domain

Rejected for the current model.

ContextNote, TraderNote, SalesNote, and similar operational text do not determine RfqCase validity and therefore remain outside the aggregate unless a later concrete invariant gives them Domain meaning.

### Repricing state

Rejected.

Working price preparation is not itself a durable Case state.

### Orthogonal state axes

Rejected.

The final state hierarchy encodes only combinations that materially change valid operations or invariants.

### Quote outcome attached directly to Quote

Rejected/refined into QuotePresentation + PresentationOutcome.

A quote value and the event of presenting it to the customer are not the same business fact.

### Presentation removed as a separate concept

Reversed during the discussion.

Once Presentation had its own business date/identity and could be the target of Hit/Away, eliminating it made the model less faithful rather than simpler.

### Generic OutcomeId

Rejected.

PresentationId already gives the necessary identity for the effective Presentation outcome.

### NaiveBusinessDate as "guaranteed business day"

Refined.

The required concept was a BusinessEntity-local calendar date. Whether that date is an allowed business day is external policy, so the canonical concept became BusinessEntityLocalDate.

### Pricing/settlement date coupling from this session

The positive-flow session established that settlement determination belongs in Terms while pricing-round date context belongs in PricingEpisode. However, the exact base-date model discussed at this stage was later found to conflate pricing date and assumed trade date.

Session 02 corrected that by introducing AssumedTradeDate and TradeDateLag. Do not revive the earlier PricingDate-based settlement interpretation from historical discussion.

## Intentionally unresolved at session close

The following topics were deliberately left open rather than guessed into the positive-flow model:

- RfqDraft structure, lifecycle, copy/seed/publication behavior, and ownership fields;
- correction, reversal, and historical amendment semantics;
- exact CancellationReason taxonomy;
- whether Presented-Away Case closure needs a separate typed Case-level disposition/reason;
- future typed outcome/feedback categories for statistics and analytics;
- detailed Application use cases and authorization policy;
- exact persistence/history schema;
- foreign-market settlement-date context beyond the currently justified model;
- settlement amount/currency/FX-conversion semantics;
- whether provenance/lineage should later become a first-class Domain concept.

The session also recognized that "Client withdrew," "No response," "Price rejected," "Traded elsewhere," and an operationally late close may eventually require different typed classifications. They were not forced into the current free-form Feedback fields because the business/statistical requirement was not yet sufficiently known.

## Canonical documentation updated

At the end of this design unit, the RfqCase positive-flow model was written into the canonical Domain documentation.

The current `../../domain.md` has since been extended by Session 02, especially around:

- RfqDraft;
- AssumedTradeDate;
- TradeDateLag settlement;
- date invariants;
- NotionalAmount/CleanPrice/Rate value semantics.

Those later refinements are authoritative. This record preserves why the RfqCase positive-flow model took its shape; it is not a frozen alternative specification.

## Why this design unit was considered complete

Before moving on, the RfqCase model was re-walked across the full positive-flow state machine.

The final review checked that:

- every Open state had coherent normal transitions;
- current Terms, Episode, FirmQuote, and Presentation references formed a valid chain;
- customer presentation and customer outcome were not conflated with quote creation;
- Hit/Away and close semantics worked both with and without an existing Presentation outcome;
- continued negotiation after Away remained representable;
- changing pricing context did not incorrectly carry a FirmQuote from an older Episode;
- WorkingQuote could remain outside Domain without losing the real "work a new price while old Presented quote is live" workflow;
- historical facts needed by current operations were available without requiring full-history aggregate loading;
- unresolved exception/correction questions could be deferred without changing the positive-flow state model.

After that re-scan, no remaining positive-flow contradiction required reopening the model.

The unit was therefore stable enough to stop expanding RfqCase and move to the pre-publication boundary.

## Boundary handed to Session 02 — RfqDraft

The next design unit was given a deliberately narrow boundary:

- treat the positive-flow RfqCase model as settled unless Draft requirements exposed a concrete contradiction;
- do not add Draft back into RfqCase.State;
- model pre-publication work separately;
- determine how an incomplete/editable Draft becomes a fully valid RfqCase;
- determine copy/seed behavior for creating new RFQ work from an existing Draft or Case;
- keep actor authorization separate from Domain validity;
- stop and revisit RfqCase only when a concrete Draft requirement invalidates an earlier rationale.

That boundary led directly to the separate RfqDraft Aggregate Root designed in Session 02.
