# Bond RFQ — Domain

This document is the active canonical authority for the RFQ Domain.

It intentionally describes the target business model, not necessarily the current implementation. The current code base still contains concepts from the previous model. When code and this document disagree on domain meaning, this document is authoritative until the implementation is migrated.

Historical documents are retained under docs/_archive/. They are useful context, but they are not active authority.

Git history is the version history of this document.

## 1. Scope

This document defines the Domain model for an internal Bond RFQ workflow.

The model is centered on the internal coordination between:

- the **Draft Owner**, who owns pre-publication responsibility for an RfqDraft;
- the **Contact Owner**, who owns the customer-facing RFQ interaction; and
- the **Quote Owner**, who owns pricing responsibility for the RFQ.

Draft Owner is a pre-publication concept. It is not carried into RfqCase when a Draft is published.

The Domain does not assume that the entire customer interaction is conducted electronically through this system. Customer communication may occur through external channels, while this system records and coordinates the internally relevant business facts such as pricing rounds, firm quotes, customer presentations, presentation outcomes, and Case closure.

Accordingly, this is not intended to model an electronic RFQ protocol or trading venue end-to-end. External communication protocols, venue-specific auction mechanics, message delivery, and booking are outside the current Domain unless they introduce business facts or invariants that must be represented internally.

The model is not intentionally restricted to JPY corporate bonds. However, it does not claim to be a complete or generic model for every bond market, product type, settlement convention, or RFQ workflow.

The Domain should represent only business concepts and invariants supported by concrete requirements. New markets, products, or channels may extend the model when they introduce genuinely different Domain concepts or rules; the existing model should not be generalized speculatively in anticipation of such requirements.

Accordingly:

- bond-specific concepts are acceptable where they reflect the current business;
- market-specific rules should be introduced when required rather than assumed universally;
- abstractions should be generalized only when multiple concrete requirements demonstrate a common Domain concept;
- absence of a feature from this document means that it is currently outside the modeled scope, not that the feature is permanently prohibited.

The RFQ lifecycle ends at RFQ outcome. Booking/ticket creation is downstream and is not part of this Domain.

## 2. Design intent

The design has two goals that must be kept together:

1. make invalid business states difficult or impossible to represent;
2. avoid turning every workflow detail, actor distinction, UI operation, or audit fact into a Domain concept.

The current model therefore distinguishes:

- durable business facts from workflow/UI convenience;
- Domain state validity from actor authorization;
- one business operation from another based on semantic effect, not on who invoked it;
- one valid RfqCase business state from the operational revision in which that state occurred;
- retained operational chronology from durable TraceRecord that may outlive that chronology;
- Domain history requirements from implementation loading strategy: a history-dependent operation does not imply eager loading or whole-history rewrite for every operation;
- absolute time from Business-Entity-local calendar dates;
- a responsibility owner from the ActorId that actually performed a durable business action;
- a quote from the act of presenting that quote;
- the outcome of a presentation from the final closure of the Case.

## 3. Domain/Application boundary

### 3.1 Domain owns validity and deterministic business meaning

An operation belongs in Domain when its meaning or invariant is required to prevent an invalid RFQ state.

Examples:

- a Presented state must point to the same Quote as the current FirmQuote;
- a Hit must apply to the current presentation and must occur no later than FirmQuote.ValidUntil;
- changing AssumedTradeDate creates a new immutable PricingEpisode and does not carry a current FirmQuote into the new Episode;
- a PricingEpisode change creates a new immutable PricingEpisode rather than mutating the previous one;
- a PricingEpisode created for changed Terms must refer to the new RfqTerms;
- a Close-as-Away from a Negotiating state must use the latest presentation and must ensure that presentation has an Away outcome.

### 3.2 Application owns authorization, orchestration, and external context

Domain operations are not split merely because different actors, roles, desks, or approval routes invoke them.

Examples:

- a user handoff, a management reassignment, and a takeover workflow may all ultimately invoke the same Domain ownership-change operation if their Domain effect is identical;
- authorization such as "only the Contact Owner may present" belongs in Application;
- resolving the current authenticated actor belongs in Application, while a supplied ActorId may become a Domain fact when the identity of the actual committer/presenter/closer/canceller is part of durable business meaning;
- current BusinessEntityLocalDate, external calculation, ID allocation, persistence, transaction management, and purely technical audit metadata belong outside Domain;
- an Application use case may compose multiple Domain operations atomically.

Conversely, two operation variants or typed reasons may share the same source and target state while preserving different business meaning. Invalidation because validity elapsed and explicit withdrawal are represented as distinct QuoteInvalidationReason values under one InvalidateQuote operation.

### 3.3 Application composites do not redefine Domain semantics

A composite use case may execute several Domain operations in sequence.

If multiple operations create a new PricingEpisode, each operation creates its own immutable episode. Intermediate episodes are valid even when no Quote was created from them. The final episode becomes current. Historical persistence may expose the intermediate episodes.

Do not reinterpret "number of PricingEpisodes" as "number of quotes actually priced."

### 3.4 Explicitly Domain-external or deferred for now

The following remain outside the positive-flow RfqCase state itself:

- actor/role authorization and CurrentUser;
- purely technical audit metadata beyond explicit Domain business facts;
- WorkingQuote and other mutable pricing work-in-progress;
- ContextNote, TraderNote, and SalesNote;
- UI state;
- RfqDraft lifecycle/editing, which belongs to the separate RfqDraft model rather than RfqCase.State;
- eager in-memory ownership of complete historical collections of Terms, Episodes, Quotes, Presentations, and Outcomes;
- booking;
- final settlement amount/currency/FX-conversion rules.

Operational correction is no longer wholly Domain-external: operational Restore, RfqCaseRevision chronology, and durable TraceRecord are defined below.

The ordinary replayable CaseOperation language is canonicalized below and is used by accepted revision provenance. The broader historical-correction construction model, including any Trace-native EffectiveCommand language, remains deliberately deferred.

## 4. Naming and notation

The domain documentation uses C#-compatible PascalCase for type names, field names, and operation names. This is intentional so that the conceptual model maps cleanly to the implementation.

Use:

- RfqCase, RfqDraft, RfqTerms, PricingEpisode;
- CaseId, DraftId, QuoteId, PresentationId;
- CommitQuote, PresentQuote, RequestRepricingOnAway, PublishDraft.

Do not mix snake_case into the domain vocabulary.

Use Id rather than ID in type/member names.

Variation names should describe the semantic variant, not implementation mechanics.

Examples:

- SettlementDateRule.ExplicitDate;
- SettlementDateRule.TradeDateLag;
- PresentationOutcome.Hit;
- PresentationOutcome.Away;
- CaseOutcome.Presented;
- CaseOutcome.Unpresented.

## 5. Identity model

CaseId identifies one longitudinal RFQ across its operational revisions. Every RfqCase state and every RfqCaseRevision in that chronology carries the same CaseId.

RfqDraft has DraftId. DraftId and CaseId are distinct identity types; publication creates a new CaseId rather than reusing DraftId.

The current model deliberately does not require an additional RfqCaseRevisionId. One operational revision is identified conceptually by:

    (CaseId, CaseVersionNumber)

TraceRecord also has no separate TraceId in the current Domain model. Persistence may add storage keys or sequence metadata without turning them into Domain identity unless later business rules require such identity.

Child identity inside an RfqCase is Case-local unless explicitly stated otherwise.

Current Case-local IDs:

- RfqTermsId;
- PricingEpisodeId;
- QuoteId;
- PresentationId.

The complete persistent identity of a child is therefore conceptually (CaseId, LocalId).

No separate OutcomeId is introduced. A Presentation outcome is identified by its PresentationId because one Presentation has at most one effective outcome.

No separate typed reference is required for a Presentation Away outcome. Where a later Domain fact needs the Away result itself, it carries the immutable PresentationAwayOutcome value, which already identifies the Presentation through PresentationId.

## 6. Scalar and supporting Domain Objects

### 6.1 BusinessEntity

BusinessEntity is a Domain Object whose underlying representation may be a string.

It identifies the organizational/business entity that owns an RfqCase or RfqDraft and provides the local-date context for that aggregate's BusinessEntityLocalDate values.

BusinessEntity is immutable for both RfqCase and RfqDraft. No normal Domain operation changes it.

The exact vocabulary of BusinessEntity is intentionally not fixed here.

### 6.2 BusinessEntityLocalDate

BusinessEntityLocalDate is a Domain Object representing a local calendar date in the context of the owning RfqCase.BusinessEntity or RfqDraft.BusinessEntity.

Important:

- it does not itself prove that the entity is open for business on that date;
- it does not carry timezone or calendar metadata;
- "is this an allowed operating day?" is resolved by Application/external calendar context;
- it is not an absolute timestamp.

This replaces the earlier NaiveBusinessDate terminology.

### 6.3 Timepoint

Timepoint is a Domain Object representing one absolute point on the time axis.

The Domain model must not expose DateTimeOffset as the conceptual type. C# may use DateTimeOffset or another suitable primitive internally at the implementation boundary.

Current uses include:

- FirmQuote.ValidUntil;
- Quote.CommittedAt;
- QuotePresentation.PresentedAt;
- PresentationHitOutcome.HitAt;
- TerminalState.Closed.ClosedAt;
- TerminalState.Cancelled.CancelledAt;
- ReopenOperation.ReopenedAt;
- TraceRecord.RecordedAt.

### 6.4 ActorId

ActorId identifies the actual human or service actor that performed a durable business action.

ActorId is distinct from responsibility identities such as ContactOwnerId and QuoteOwnerId. For example, a dealer may remain QuoteOwner while a Sales user or an automated service commits a price that the dealer authorized.

The current model does not split ActorId into Human/System variants. Introduce actor-kind variants only if actor kind itself becomes business-significant.

### 6.5 CityCalendarSymbol

CityCalendarSymbol is a Domain Object backed by a string and identifies a city/business calendar used by settlement-date lag calculation.

### 6.6 NotionalAmount

NotionalAmount is a Domain Object representing the numeric notional/face amount used by the RFQ.

    NotionalAmount
    - Value : decimal >= 0

NotionalAmount does not carry currency. The security determines the denomination context; duplicating currency on NotionalAmount would introduce a second potentially conflicting source of truth.

### 6.7 CleanPrice

CleanPrice is a Domain Object representing a clean-price quote value.

Its exact underlying representation is intentionally not fixed here. No numeric range invariant is currently imposed. No generic Price Domain Object is introduced by this decision.

### 6.8 Rate

Rate is a Domain Object representing a rate level used by rate-valued Domain facts such as Yield quotes.

No numeric range invariant is currently imposed. Exact underlying representation and external unit conversion are implementation/boundary concerns unless a future business requirement makes them Domain-significant.

### 6.9 Feedback

Feedback is currently free-form optional string data.

This is deliberately provisional. Future statistics/analytics may require a typed disposition plus optional free text. Do not infer a stable taxonomy from the current string.

## 7. RfqCase business-state shape

RfqCase is one complete valid business state of an RFQ. It is not, by itself, synonymous with "the latest Case"; operational currentness is represented by the current RfqCaseRevision defined in Section 23.

The current RfqCase shape is:

    RfqCase
    - CaseId
    - BusinessEntity
    - OpenDate : BusinessEntityLocalDate
    - RfqTerms : RfqTerms
    - ContactOwnerId
    - State

Semantics:

- OpenDate is the local date on which the Case was opened/initiated;
- BusinessEntity is immutable;
- RfqTerms is the current Terms entity;
- ContactOwnerId is mutable through an explicit Domain operation;
- State changes only through explicit Domain transitions.

OpenDate and the initial PricingEpisode.PricingDate normally match in ordinary creation flows, but equality is not currently a Domain invariant. The business consequence of differing values is not strong enough to reject an otherwise valid Case. Application may default them to the same date.

## 8. RfqTerms

RfqTerms is an immutable Case-local child Entity.

    RfqTerms
    - RfqTermsId
    - ClientId
    - Side
    - SecurityId
    - Notional : NotionalAmount
    - SettlementDateRule

All fields are immutable on a particular RfqTerms instance.

"Changeable" means that a Domain operation may create a new RfqTerms with a new RfqTermsId and make it current. It never means mutating an existing RfqTerms.

Current same-Case change policy:

- ClientId: no normal change operation;
- Side: no normal change operation;
- SecurityId: no normal change operation;
- Notional: may change only while the Case is in Inquiry phase;
- SettlementDateRule: may change only while the Case is in Inquiry phase.

After the first customer presentation, a change to Notional or SettlementDateRule creates a new Case rather than changing the existing Case. SeedDraftFromCase supports starting that new-RFQ workflow from the existing Case.

### 8.1 SettlementDateRule

SettlementDateRule answers "how is the settlement date determined?"

    SettlementDateRule
    = ExplicitDate(BusinessEntityLocalDate)
    | TradeDateLag(SettlementLag)

ExplicitDate means the explicitly supplied BusinessEntity-local date is the settlement date. It is independent of PricingDate and AssumedTradeDate changes.

TradeDateLag expresses settlement relative to trade date. While the RFQ is open, settlement date is resolved from PricingEpisode.AssumedTradeDate using SettlementLag. AssumedTradeDate is an explicit pricing assumption, not the eventual executed TradeDate. Changing it can therefore change the resolved settlement date without creating new RfqTerms.

There is intentionally no separate SettlementDate Domain Object in the current scope.

### 8.2 SettlementLag

    SettlementLag
    - BusinessDayCount : integer >= 0
    - CalendarSymbols : non-empty CityCalendarSymbol[]

Semantics:

- all listed calendars participate;
- if any listed calendar treats a day as a holiday, that day is unavailable;
- the convention is Following;
- BusinessDayCount may be zero;
- the resolved result is represented as BusinessEntityLocalDate.

The BusinessEntity-local date type does not guarantee that the owning entity itself is open on that day. Application/external calendar logic owns operating-day validity.

Final settlement amount, settlement currency, and FX conversion are future extensions and are deliberately separate from SettlementDateRule.

## 9. PricingEpisode

PricingEpisode is an immutable Case-local child Entity.

    PricingEpisode
    - PricingEpisodeId
    - RfqTermsId
    - QuoteOwnerId
    - PricingDate : BusinessEntityLocalDate
    - AssumedTradeDate : BusinessEntityLocalDate
    - Origin : PricingEpisodeOrigin

Meaning:

A PricingEpisode is one logical pricing round for one RfqTerms, owned by one QuoteOwner, attributed to one PricingDate, and evaluated under one AssumedTradeDate.

PricingDate and AssumedTradeDate are distinct business concepts:

- PricingDate is the local date to which the pricing round belongs;
- AssumedTradeDate is the trade date assumed when evaluating trade-date-dependent terms such as TradeDateLag settlement;
- equality between PricingDate and AssumedTradeDate is not a Domain invariant.

It is not a complete snapshot of every screen value or every piece of information the trader saw.

In particular it does not contain:

- WorkingQuote;
- notes;
- calculations;
- UI state;
- audit actor/time.


### 9.1 PricingEpisodeOrigin

    PricingEpisodeOrigin
    = Initial
    | RfqTermsChanged(PreviousPricingEpisodeId)
    | QuoteOwnerChanged(PreviousPricingEpisodeId)
    | PricingDateRolled(PreviousPricingEpisodeId)
    | AssumedTradeDateChanged(PreviousPricingEpisodeId)
    | RepricingRequested(
          PreviousPricingEpisodeId,
          QuoteId,
          Feedback?
      )
    | Away(
          PreviousPricingEpisodeId,
          PresentationAwayOutcome
      )
    | ReopenedFromAway(
          PreviousPricingEpisodeId,
          PresentationAwayOutcome
      )
    | ReopenedFromUnpresented(
          PreviousPricingEpisodeId,
          Feedback?
      )
    | ReopenedFromCancellation(
          PreviousPricingEpisodeId,
          PriorPresentation?
      )

Origin is exactly one variant, not a bag/list of causes.

If an Application composite performs two episode-changing Domain operations, two episodes are created in sequence. The current episode is the latest one.

Origin stores only information needed to explain why the new pricing round exists. Most variants identify the previous PricingEpisode so Episode lineage remains explicit.

Away additionally retains the immutable PresentationAwayOutcome that caused an in-place continuation of pricing after the current Presentation went Away. ReopenedFromAway retains the genuine terminal Away outcome from which the same Case later resumed. ReopenedFromUnpresented retains any Feedback from the genuine unpresented close. ReopenedFromCancellation retains the prior Presentation context, when one existed, because a genuine withdrawal does not erase customer-facing pricing facts that may remain relevant when the same negotiation resumes.

The operation that creates an Away-origin Episode is RequestRepricingOnAway. Reopen operations create the corresponding ReopenedFrom... origins. Operation names describe business actions; PricingEpisodeOrigin describes the business fact from which the new Episode originated.

## 10. Quote and FirmQuote

Quote is an immutable Case-local child Entity.

    Quote
    - QuoteId
    - PricingEpisodeId
    - Value : QuoteValue
    - CommittedBy : ActorId
    - CommittedAt : Timepoint

Current QuoteValue:

    QuoteValue
    = CleanPrice(CleanPriceValue : CleanPrice)
    | Yield(
          YieldRate : Rate,
          YieldConventionId
      )

CommittedBy and CommittedAt record who actually committed the Quote and when that commitment occurred. They are durable business facts and do not replace QuoteOwnerId, which remains pricing responsibility on PricingEpisode.

A Quote means a price condition produced in a specific PricingEpisode. It does not by itself mean that the price is current, firm, or customer-visible.


### 10.1 FirmQuote

    FirmQuote
    - Quote
    - ValidUntil : Timepoint

FirmQuote is current-state data meaning that the Quote is currently firm according to the currently recorded validity.

At most one FirmQuote is current in a Case.

ValidUntil is not part of Quote identity. ExtendValidity may move ValidUntil later while retaining the same QuoteId.

Time passing does not automatically mutate Domain state. An expired FirmQuote may therefore remain structurally current until another explicit operation changes the Case.

ExtendValidity may be accepted even after the previously recorded ValidUntil has elapsed. Its current invariant is only:

    NewValidUntil > CurrentValidUntil

No ordering invariant is currently imposed between ExtendedAt and NewValidUntil.

InvalidateQuote with Reason=Expired represents explicit Domain recognition that the current FirmQuote is being invalidated because its validity elapsed.

## 11. QuotePresentation

QuotePresentation is an immutable Case-local child Entity.

    QuotePresentation
    - PresentationId
    - QuoteId
    - PresentationDate : BusinessEntityLocalDate
    - PresentedBy : ActorId
    - PresentedAt : Timepoint

A QuotePresentation represents a business proposal event: the Quote was actually presented to the customer.

PresentedBy and PresentedAt record the actor and absolute time of that presentation action. They are distinct from ContactOwnerId, which represents customer-contact responsibility.

Quote and QuotePresentation are deliberately separate because:

- a Quote may never be presented;
- the same Quote could, in future workflows, be presented in more than one distinct business proposal event;
- downstream Hit/Away facts apply to the presentation, not merely to the price value.

Repeated technical delivery/retry does not create another Presentation. A new PresentationId represents a new business proposal event.

The positive-flow model currently creates a new Presentation when PresentQuote is executed.

## 12. Presentation outcomes

A Presentation may have at most one effective PresentationOutcome.

    PresentationOutcome
    = Hit(PresentationHitOutcome)
    | Away(PresentationAwayOutcome)

No independent OutcomeId exists.

### 12.1 PresentationHitOutcome

    PresentationHitOutcome
    - PresentationId
    - HitDate : BusinessEntityLocalDate
    - HitAt : Timepoint

Meaning:

- HitDate is the BusinessEntity-local date on which the customer and desk agreed;
- HitAt is the absolute timepoint used for validity checks.

Hit is terminal for the Case in normal flow.


### 12.2 PresentationAwayOutcome

    PresentationAwayOutcome
    - PresentationId
    - AwayDate : BusinessEntityLocalDate
    - RecordedBy : ActorId
    - RecordedAt : Timepoint
    - Feedback : string?

AwayDate is the BusinessEntity-local date to which the Presentation's Away outcome is attributed.

RecordedBy and RecordedAt identify the internal actor and absolute time at which the Away outcome was established in the Domain. They do not claim that this actor caused the customer to go Away, nor that RecordedAt is the exact external customer-event time.

For RequestRepricingOnAway, RecordedBy / RecordedAt come from RequestedBy / RequestedAt. When CloseCase records an Away outcome as part of closure, they come from ClosedBy / ClosedAt.

Away is not necessarily terminal for the Case. RequestRepricingOnAway may establish the Away outcome and create a new PricingEpisode.

AwayDate is not required to equal PresentationDate, PricingDate, or the local date corresponding to RecordedAt.


## 13. Case outcome and terminal state

The outcome of a valid RFQ Case is modeled separately from cancellation.

    CaseClose
    - CloseDate : BusinessEntityLocalDate
    - ClosedBy : ActorId
    - ClosedAt : Timepoint

    CancellationReason
    = Withdrawn
    | CreatedInError

    Cancellation
    - CancellationDate : BusinessEntityLocalDate
    - CancelledBy : ActorId
    - CancelledAt : Timepoint
    - Reason : CancellationReason

    CaseOutcome
    = Presented(PresentationOutcome)
    | Unpresented(Feedback?)

    PriorPresentation
    - Presentation : QuotePresentation
    - AwayOutcome : PresentationAwayOutcome?

    TerminalPricingContext
    - PricingEpisode
    - PriorPresentation : PriorPresentation?

    TerminalState
    = Closed(
          PricingContext : TerminalPricingContext,
          Close : CaseClose,
          Outcome : CaseOutcome
      )
    | Cancelled(
          PricingContext : TerminalPricingContext,
          Cancellation
      )

TerminalPricingContext is the pricing context from which the Case terminated. It is a business fact of the Terminal state, not a Reopen-specific cache. It is therefore retained for Hit, Away, Unpresented, and Cancelled terminal states even when normal positive flow does not later reopen that terminal occurrence.

Semantics:

- Closed means a valid RFQ Case reached a normal business conclusion;
- Cancelled means the Case terminated without an ordinary CaseOutcome;
- Withdrawn means a genuine Case was withdrawn/stopped and may later be eligible to reopen as the same negotiation context;
- CreatedInError means the Case itself should not have been established as genuine business activity, including duplicate or mistaken creation cases; it is not reopenable in normal positive flow.

CloseDate is the BusinessEntity-local date the Case itself was closed. ClosedAt is the absolute time of the closing action and ClosedBy is the internal actor that performed it. These are distinct from HitDate and AwayDate.

CancellationDate is likewise the BusinessEntity-local cancellation date, while CancelledAt and CancelledBy record the cancellation action time and actor.

Examples:

- a customer may Hit on one date and operational Case closure may be recorded later;
- a Presentation may go Away, pricing may continue, and the Case may be closed on a later date;
- a genuine Withdrawn cancellation may later be reopened without implying that the cancellation was mistaken.

### 13.1 Terminal pricing context

TerminalPricingContext captures only the current pricing lineage needed to describe the Terminal Case state. It does not preserve a FirmQuote.

When an Open state becomes Terminal:

- Inquiry.Pricing / Inquiry.PendingPresentation produce PriorPresentation=None;
- Negotiating.Pricing / Negotiating.PendingPresentation preserve LatestPresentation and LatestPresentationAwayOutcome;
- Negotiating.Presented preserves the current Presentation;
- CloseCase(Away) stores the final Away outcome for the Presentation in PriorPresentation.AwayOutcome, whether that outcome was already recorded or was established by the CloseCase operation;
- Cancel preserves the source Open state's prior Presentation context without manufacturing an outcome.

A current FirmQuote is never carried into TerminalPricingContext.

### 13.2 Presented Case close and Away outcomes

When a Negotiating Case closes Away:

- the outcome applies to the latest QuotePresentation;
- if that Presentation already has a PresentationAwayOutcome, CloseCase uses the already-recorded outcome;
- otherwise CloseCase records the PresentationAwayOutcome as part of closure;
- the resulting CaseOutcome is Presented(Away(...)) in either path;
- TerminalPricingContext.PriorPresentation contains that same Presentation and Away outcome.

Whether the Away outcome was already recorded is an operation-construction distinction, not a different business kind of Away.

### 13.3 Unpresented close

A Case that has never reached a customer Presentation closes as:

    CaseOutcome.Unpresented(Feedback?)

There is no PresentationOutcome because no Presentation exists, and TerminalPricingContext.PriorPresentation is None.

### 13.4 Genuine reopen after terminal business

A genuinely correct terminal occurrence may later be followed by resumed business on the same CaseId when the operator/Application asserts that it is the same negotiation context.

This is distinct from Operational Restore:

- Reopen means the terminal occurrence was genuine and remains durable business activity, but the same negotiation later resumes;
- Restore means the prior operational path was mistaken and an earlier valid Open revision is selected instead.

Normal positive-flow Reopen applicability is:

- Closed(Presented(Away(...))) -> ReopenAway -> Negotiating.Pricing;
- Closed(Unpresented(...)) -> ReopenUnpresented -> Inquiry.Pricing;
- Cancelled(Reason=Withdrawn) -> ReopenCancellation -> Inquiry.Pricing when PriorPresentation=None, otherwise Negotiating.Pricing;
- Closed(Presented(Hit(...))) is not reopenable through ordinary positive flow;
- Cancelled(Reason=CreatedInError) is not reopenable through ordinary positive flow.

Every Reopen creates a fresh PricingEpisode. It never restores a previous FirmQuote.

### 13.5 Deferred close classification

The model does not yet carry a separate Case-level Away classification/reason for a Presented Case after the latest Presentation already went Away.

Examples of currently unresolved future classifications include:

- ClientWithdrew;
- NoResponse / unresolved;
- PriceRejected;
- TradedElsewhere;
- late operational close / "forgot to close."

These may become typed statistical dispositions and may need to distinguish customer business outcome from operational close reason.

Do not overload PresentationAwayOutcome.Feedback with this separate future meaning.

## 14. State model

Draft is not an RfqCase state.

RfqCase begins only after a QuoteOwner has been established and the Case is published/created into the RFQ Domain.

Conceptual hierarchy:

    RfqCaseState
    ├─ Open
    │  ├─ Inquiry
    │  │  ├─ Pricing
    │  │  └─ PendingPresentation
    │  └─ Negotiating
    │     ├─ Pricing
    │     ├─ PendingPresentation
    │     └─ Presented
    └─ Terminal
       ├─ Closed
       └─ Cancelled

Inquiry means no QuotePresentation has ever occurred in the Case.

Negotiating means at least one QuotePresentation has occurred.

Pricing means no current FirmQuote exists and pricing responsibility is with the QuoteOwner side. It does not assert that a trader is actively typing or calculating at that instant.

PendingPresentation means a current FirmQuote exists but that FirmQuote has not yet been presented to the customer.

Presented means the current FirmQuote is represented by the current QuotePresentation and is normally eligible for Hit subject to validity checks.

There is deliberately no Domain Repricing state. Working on another price while an existing FirmQuote remains presented is Application/UI workflow. Domain remains Presented until a new FirmQuote replaces the old one, the old one is withdrawn/expired, or a presentation outcome occurs.

## 15. Exact state fields

### 15.1 Inquiry.Pricing

    Inquiry.Pricing
    - PricingEpisode

### 15.2 Inquiry.PendingPresentation

    Inquiry.PendingPresentation
    - PricingEpisode
    - FirmQuote

### 15.3 Negotiating.Pricing

    Negotiating.Pricing
    - PricingEpisode
    - LatestPresentation : QuotePresentation
    - LatestPresentationAwayOutcome : PresentationAwayOutcome?

The optional Away outcome is business-significant current information, not merely an implementation cache. It tells the Domain whether the latest customer proposal has already gone Away and is required by normal close behavior.

### 15.4 Negotiating.PendingPresentation

    Negotiating.PendingPresentation
    - PricingEpisode
    - FirmQuote
    - LatestPresentation : QuotePresentation
    - LatestPresentationAwayOutcome : PresentationAwayOutcome?

The current FirmQuote is not yet presented. LatestPresentation refers to the prior customer proposal.

### 15.5 Negotiating.Presented

    Negotiating.Presented
    - PricingEpisode
    - FirmQuote
    - Presentation : QuotePresentation

The Presentation itself is the latest/current customer proposal, so a duplicated LatestPresentation field is unnecessary.

## 16. Core invariants

### 16.1 Current Terms chain

For every Open state:

    RfqCase.RfqTerms.RfqTermsId
    == State.PricingEpisode.RfqTermsId

For every Terminal state:

    RfqCase.RfqTerms.RfqTermsId
    == TerminalState.PricingContext.PricingEpisode.RfqTermsId

Historical PricingEpisodes may refer to historical RfqTerms entities. The current Open state or TerminalPricingContext always refers to the current RfqTerms.

### 16.2 Current Quote chain

Whenever a FirmQuote exists:

    State.PricingEpisode.PricingEpisodeId
    == FirmQuote.Quote.PricingEpisodeId

A FirmQuote from an older PricingEpisode is never carried into a new current PricingEpisode.

### 16.3 Presented chain

In Negotiating.Presented:

    Presentation.QuoteId
    == FirmQuote.Quote.QuoteId

### 16.4 Latest Presentation Away outcome

When LatestPresentationAwayOutcome exists:

    LatestPresentationAwayOutcome.PresentationId
    == LatestPresentation.PresentationId

An Open Negotiating state cannot carry a Hit outcome. Hit is terminal.

### 16.5 Hit validity

Hit is allowed only from Negotiating.Presented.

For a normal Hit:

    PresentationHitOutcome.PresentationId
    == Presentation.PresentationId

    PresentationHitOutcome.HitAt
    <= FirmQuote.ValidUntil

Normal chronology requires PresentationHitOutcome.HitDate not to precede Presentation.PresentationDate.

PricingDate, PresentationDate, and HitDate are distinct business facts. The Domain does not require equality between them.


### 16.6 Away timing

AwayDate need not equal PricingDate or PresentationDate.

Normal chronology requires the Away outcome not to precede its Presentation.

PresentationAwayOutcome.RecordedAt is the internal recording/establishment time of the Away fact, not an asserted external customer-event time. No equality or ordering relation between AwayDate and the local date corresponding to RecordedAt is currently required beyond the normal business chronology constraints on AwayDate.

### 16.7 Close timing

CaseClose.CloseDate is separate from outcome date.

In normal flow, a Presented outcome must exist no later than CaseClose.CloseDate.

Cancellation.CancellationDate is the date of Case cancellation, not a Presentation outcome date.

### 16.8 PricingDate roll

RollPricingDate creates a new PricingEpisode.

Normal-flow roll requires:

    NewPricingDate > PreviousPricingDate

The Domain does not discover "today" itself. Application supplies the BusinessEntityLocalDate.

RollPricingDate preserves AssumedTradeDate.

### 16.9 AssumedTradeDate change

ChangeAssumedTradeDate creates a new PricingEpisode.

Normal-flow change requires:

    NewAssumedTradeDate != PreviousAssumedTradeDate

PricingDate is preserved. The new Episode does not carry a FirmQuote from the previous Episode, so a Quote must be reaffirmed in the new Episode before it can become current and firm again. This does not imply that the numerical Quote value must change; for example, an ExplicitDate settlement rule may be unaffected by the AssumedTradeDate change.

### 16.10 Settlement and AssumedTradeDate consistency

For every Open state, when the current RfqTerms uses ExplicitDate:

    RfqTerms.SettlementDateRule = ExplicitDate(SettlementDate)

the following must hold:

    SettlementDate >= State.PricingEpisode.AssumedTradeDate

TradeDateLag does not require an equivalent cross-field check because SettlementLag has a non-negative BusinessDayCount and Following semantics.

This invariant applies to every Domain operation that can establish a new current Terms/Episode combination, including PublishDraft, ChangeRfqTerms, and ChangeAssumedTradeDate.


### 16.11 Time does not mutate state

Clock passage alone does not change Domain state.

An expired FirmQuote may remain structurally current until InvalidateQuote(Reason=Expired) or another explicit operation changes the Case.

InvalidateQuote(Reason=Expired) requires:

    InvalidatedAt >= FirmQuote.ValidUntil

Hit always checks HitAt against the current FirmQuote.ValidUntil, so delayed expiry processing cannot allow an invalid Hit.

ExtendValidity may extend a structurally current FirmQuote even when ExtendedAt is later than the previous ValidUntil, provided:

    NewValidUntil > CurrentValidUntil


### 16.12 Terminal pricing and Reopen invariants

Terminal-state consistency requires:

    Closed(Unpresented)
        => PricingContext.PriorPresentation == None

    Closed(Presented(Away(away)))
        => PricingContext.PriorPresentation == Some(prior)
        && prior.Presentation.PresentationId == away.PresentationId
        && prior.AwayOutcome == Some(away)

    Closed(Presented(Hit(hit)))
        => PricingContext.PriorPresentation == Some(prior)
        && prior.Presentation.PresentationId == hit.PresentationId

Cancelled preserves the source Open state's Presentation context and does not manufacture Hit/Away.

Reopen always creates a new PricingEpisode with:

    RfqTermsId       = source current RfqTerms.RfqTermsId
    QuoteOwnerId     = source TerminalPricingContext.PricingEpisode.QuoteOwnerId
    PricingDate      = ReopenDate
    AssumedTradeDate = NewAssumedTradeDate

The new Episode uses the corresponding ReopenedFrom... PricingEpisodeOrigin and carries no FirmQuote.

No Domain chronology invariant requires ReopenDate or ReopenedAt to be on or after the preceding CloseDate / CancellationDate / ClosedAt / CancelledAt.


## 17. RfqCase Domain operations

### 17.1 Operation model

Ordinary RfqCase business operations are first-class Domain Objects.

Conceptually, the underlying state transition is a partial function:

    Apply :
        RfqCase x CaseOperation
        -> RfqCase

"Partial" means that an operation is valid only for its documented source-state shapes and invariants. CaseOperation is not an unrestricted function over every RfqCase variant.

The function-like model is the canonical semantic definition. The state graph in Section 18 is retained as a state-centric view of the same semantics because it is useful for understanding lifecycle reachability.

Application request models are not CaseOperation values. Application resolves external context before constructing a fully resolved Domain Operation, including supplied/generated Case-local identities, business ActorId/Timepoint values, business dates, and other exogenous arguments required by the operation.

A CaseOperation:

- is the same Domain value whether used by live positive flow or retained/replayed from operational history;
- contains every exogenous argument required to deterministically apply that operation to a valid source RfqCase;
- does not duplicate values that are deterministically derivable from the source RfqCase merely for replay convenience;
- contains business actor/time facts where the accepted business action itself has such facts;
- has no hidden dependency on current user, clock, ID allocation, database state, or external services.

Conceptually:

    application request
      + resolved actor/time/date
      + allocated IDs
      + external context
        -> fully resolved CaseOperation
        -> Domain application

Current operation hierarchy:

    CaseOperation
    = Open(OpenOperation)
    | Terminal(TerminalOperation)
    | Reopen(ReopenOperation)

OpenOperation maps Open -> Open. TerminalOperation maps Open -> Terminal. ReopenOperation maps Terminal -> Open.

PublishDraft is not a CaseOperation. It creates the initial RfqCaseRevision with Published(DraftId). Operational Restore is also separate because it selects an earlier revision rather than applying an ordinary business operation to the current Case state.

### 17.2 OpenOperation shapes

    OpenOperation
    = CommitQuote(CommitQuote)
    | ReplaceFirmQuote(ReplaceFirmQuote)
    | InvalidateQuote(InvalidateQuote)
    | RequestRepricing(RequestRepricing)
    | ChangeRfqTerms(ChangeRfqTerms)
    | ChangeQuoteOwner(ChangeQuoteOwner)
    | RollPricingDate(RollPricingDate)
    | ChangeAssumedTradeDate(ChangeAssumedTradeDate)
    | PresentQuote(PresentQuote)
    | ExtendValidity(ExtendValidity)
    | RequestRepricingOnAway(RequestRepricingOnAway)
    | ChangeContactOwner(ChangeContactOwner)

    CommitQuote
    - QuoteId
    - Value : QuoteValue
    - ValidUntil : Timepoint
    - CommittedBy : ActorId
    - CommittedAt : Timepoint

    ReplaceFirmQuote
    - QuoteId
    - Value : QuoteValue
    - ValidUntil : Timepoint
    - CommittedBy : ActorId
    - CommittedAt : Timepoint

    QuoteInvalidationReason
    = Expired
    | Withdrawn

    InvalidateQuote
    - InvalidatedBy : ActorId
    - InvalidatedAt : Timepoint
    - Reason : QuoteInvalidationReason

    RequestRepricing
    - NewPricingEpisodeId
    - Feedback?
    - RequestedBy : ActorId
    - RequestedAt : Timepoint

    ChangeRfqTerms
    - NewRfqTermsId
    - NewPricingEpisodeId
    - Notional : NotionalAmount
    - SettlementDateRule
    - ChangedBy : ActorId
    - ChangedAt : Timepoint

    ChangeQuoteOwner
    - NewPricingEpisodeId
    - NewQuoteOwnerId
    - ChangedBy : ActorId
    - ChangedAt : Timepoint

    RollPricingDate
    - NewPricingEpisodeId
    - NewPricingDate : BusinessEntityLocalDate
    - RolledBy : ActorId
    - RolledAt : Timepoint

    ChangeAssumedTradeDate
    - NewPricingEpisodeId
    - NewAssumedTradeDate : BusinessEntityLocalDate
    - ChangedBy : ActorId
    - ChangedAt : Timepoint

    PresentQuote
    - PresentationId
    - PresentationDate : BusinessEntityLocalDate
    - PresentedBy : ActorId
    - PresentedAt : Timepoint

    ExtendValidity
    - NewValidUntil : Timepoint
    - ExtendedBy : ActorId
    - ExtendedAt : Timepoint

    RequestRepricingOnAway
    - NewPricingEpisodeId
    - AwayDate : BusinessEntityLocalDate
    - Feedback?
    - RequestedBy : ActorId
    - RequestedAt : Timepoint

    ChangeContactOwner
    - NewContactOwnerId
    - ChangedBy : ActorId
    - ChangedAt : Timepoint

Generated Case-local IDs are supplied by the fully resolved operation when the result creates a new immutable child Entity. Previous/current IDs and values are omitted when they are deterministically available from the source RfqCase.

Examples:

- CommitQuote derives PricingEpisodeId from the source current Episode;
- ChangeQuoteOwner derives the previous Episode, current RfqTermsId, PricingDate, and AssumedTradeDate from the source;
- RequestRepricing derives the rejected current QuoteId and previous Episode from the source;
- RequestRepricingOnAway derives the current PresentationId and previous Episode from the source.

Operation actor/time fields are Domain business facts, not generic persistence audit metadata. They intentionally use operation-specific names rather than one generic Stamp type.

### 17.3 OpenOperation semantics

CommitQuote creates a new Quote and makes it the current FirmQuote. ReplaceFirmQuote creates a new Quote and atomically replaces the current FirmQuote without manufacturing a separate invalidation or Away outcome.

InvalidateQuote removes the current FirmQuote while preserving the current PricingEpisode. Reason=Expired requires InvalidatedAt >= current ValidUntil. Reason=Withdrawn has no expiry-time precondition.

RequestRepricing rejects the current unpresented firm price as the starting point for a new pricing round. It creates a new PricingEpisode with Origin=RepricingRequested(previous Episode, rejected QuoteId, Feedback?).

ChangeRfqTerms creates new RfqTerms and a new PricingEpisode. Only Notional and SettlementDateRule may change within the same Case; ClientId, Side, and SecurityId are preserved from the source RfqTerms.

ChangeQuoteOwner, RollPricingDate, and ChangeAssumedTradeDate each create one new PricingEpisode with the corresponding Origin. Values not changed by the operation are preserved from the source Episode.

PresentQuote creates a QuotePresentation for the current FirmQuote.

ExtendValidity preserves QuoteId and moves ValidUntil later. Its current invariant is:

    NewValidUntil > CurrentValidUntil

The operation is allowed even when ExtendedAt is later than the previous ValidUntil. No current invariant relates ExtendedAt to NewValidUntil.

RequestRepricingOnAway establishes an Away outcome for the current Presentation and creates a new PricingEpisode with Origin=Away(previous Episode, PresentationAwayOutcome). The generated PresentationAwayOutcome uses:

    RecordedBy = RequestedBy
    RecordedAt = RequestedAt

ChangeContactOwner changes only ContactOwnerId and preserves the current lifecycle-state shape and all pricing/presentation objects.

### 17.4 TerminalOperation shapes

    TerminalOperation
    = CloseCase(CloseCase)
    | Cancel(Cancel)

    CloseCase
    - Close : CaseClose
    - Outcome : CloseOutcome

    CloseOutcome
    = Hit(
          HitDate : BusinessEntityLocalDate,
          HitAt : Timepoint
      )
    | Away(AwayClosure)
    | Unpresented(Feedback?)

    AwayClosure
    = AlreadyRecorded
    | RecordNow(
          AwayDate : BusinessEntityLocalDate,
          Feedback?
      )

    Cancel
    - Cancellation : Cancellation

CloseCase has one business meaning: establish the normal final Case outcome and close the Case.

CloseCase(Hit) is valid only from Negotiating.Presented. It creates PresentationHitOutcome for the current Presentation. HitAt remains the business agreement time used for the validity invariant and is distinct from CaseClose.ClosedAt.

CloseCase(Away) applies to the latest Presentation:

- AlreadyRecorded requires the latest Presentation to already have PresentationAwayOutcome and reuses that same immutable fact;
- RecordNow requires that no Away outcome is already recorded for the latest Presentation and creates one using AwayDate / Feedback with RecordedBy=Close.ClosedBy and RecordedAt=Close.ClosedAt.

CloseCase(Unpresented) is valid only when the Case has never reached a Presentation.

Cancel creates TerminalState.Cancelled(PricingContext, Cancellation) and does not manufacture a Hit/Away outcome.

CloseCase and Cancel construct TerminalPricingContext from the source Open state as defined in Section 13.1. A current FirmQuote is not retained in that context.

The AlreadyRecorded / RecordNow distinction does not define two kinds of business Away. It states only whether the Away fact already exists in the source Case or is established by this CloseCase operation.

### 17.5 ReopenOperation shapes and semantics

    ReopenOperation
    = ReopenAway(ReopenAway)
    | ReopenUnpresented(ReopenUnpresented)
    | ReopenCancellation(ReopenCancellation)

    ReopenAway
    - NewPricingEpisodeId
    - ReopenDate : BusinessEntityLocalDate
    - NewAssumedTradeDate : BusinessEntityLocalDate
    - ReopenedBy : ActorId
    - ReopenedAt : Timepoint

    ReopenUnpresented
    - NewPricingEpisodeId
    - ReopenDate : BusinessEntityLocalDate
    - NewAssumedTradeDate : BusinessEntityLocalDate
    - ReopenedBy : ActorId
    - ReopenedAt : Timepoint

    ReopenCancellation
    - NewPricingEpisodeId
    - ReopenDate : BusinessEntityLocalDate
    - NewAssumedTradeDate : BusinessEntityLocalDate
    - ReopenedBy : ActorId
    - ReopenedAt : Timepoint

Variant-specific provenance is derived from the source Terminal state rather than duplicated in the operation payload.

ReopenAway applies only to Closed(Presented(Away(...))). It creates Negotiating.Pricing with a fresh PricingEpisode using ReopenedFromAway(previous Episode, terminal Away outcome), and preserves the prior Presentation/Away as latest customer context.

ReopenUnpresented applies only to Closed(Unpresented(...)). It creates Inquiry.Pricing with a fresh PricingEpisode using ReopenedFromUnpresented(previous Episode, Feedback?).

ReopenCancellation applies only to Cancelled(Reason=Withdrawn). It creates a fresh PricingEpisode using ReopenedFromCancellation(previous Episode, PriorPresentation). The result is Inquiry.Pricing when PriorPresentation=None and Negotiating.Pricing when PriorPresentation exists.

Every Reopen starts a fresh pricing round and never resurrects a previous FirmQuote.

Application/UI may default NewAssumedTradeDate as follows:

    previous.AssumedTradeDate == previous.PricingDate
        ? ReopenDate
        : previous.AssumedTradeDate

This is only a request-construction default. The Domain operation always carries an explicit NewAssumedTradeDate.

### 17.6 Revision-level operation API

Ordinary Open processing is:

    ApplyOpen(
        currentRevision,
        operation : OpenOperation
    ) -> nextRevision

Operations that create a durable TraceRecord use one common result shape:

    RevisionTraceResult
    - Revision : RfqCaseRevision
    - Trace : TraceRecord

Terminal processing is:

    ApplyTerminal(
        currentRevision,
        operation : TerminalOperation,
        terminalTraceContext
    ) -> RevisionTraceResult

Reopen processing is:

    ApplyReopen(
        currentRevision,
        operation : ReopenOperation,
        reopenTraceContext
    ) -> RevisionTraceResult

terminalTraceContext and reopenTraceContext contain retained/resolved historical facts plus Trace-recording context. Their exact loaded representations are implementation concerns.

The API boundary preserves the Domain invariant that an accepted Terminal or Reopen operation produces its revision and corresponding TraceRecord together.

## 18. Operation applicability and state graph

### 18.1 Applicability table

The operation definition is canonical; this table lists valid source/target state shapes and notable state-dependent effects.

| Operation | Valid source | Result | Important state-dependent effect |
| --- | --- | --- | --- |
| CommitQuote | Inquiry.Pricing | Inquiry.PendingPresentation | create current Quote/FirmQuote |
| CommitQuote | Negotiating.Pricing | Negotiating.PendingPresentation | preserve latest Presentation/outcome |
| ReplaceFirmQuote | Inquiry.PendingPresentation | Inquiry.PendingPresentation | replace unpresented current FirmQuote |
| ReplaceFirmQuote | Negotiating.PendingPresentation | Negotiating.PendingPresentation | preserve latest Presentation/outcome |
| ReplaceFirmQuote | Negotiating.Presented | Negotiating.PendingPresentation | previous Presentation becomes latest; new Quote is unpresented |
| InvalidateQuote | Inquiry.PendingPresentation | Inquiry.Pricing | remove current FirmQuote |
| InvalidateQuote | Negotiating.PendingPresentation | Negotiating.Pricing | preserve latest Presentation/outcome |
| InvalidateQuote | Negotiating.Presented | Negotiating.Pricing | current Presentation becomes latest; no Away fabricated |
| RequestRepricing | Inquiry.PendingPresentation | Inquiry.Pricing | new Episode; rejected Quote referenced by Origin |
| RequestRepricing | Negotiating.PendingPresentation | Negotiating.Pricing | new Episode; latest Presentation/outcome preserved |
| ChangeRfqTerms | Inquiry.Pricing | Inquiry.Pricing | new Terms + Episode |
| ChangeRfqTerms | Inquiry.PendingPresentation | Inquiry.Pricing | new Terms + Episode; FirmQuote not carried |
| ChangeQuoteOwner | Inquiry.Pricing | Inquiry.Pricing | new Episode |
| ChangeQuoteOwner | Negotiating.Pricing | Negotiating.Pricing | new Episode; latest Presentation/outcome preserved |
| RollPricingDate | Inquiry.Pricing | Inquiry.Pricing | new Episode |
| RollPricingDate | Inquiry.PendingPresentation | Inquiry.Pricing | new Episode; FirmQuote not carried |
| RollPricingDate | Negotiating.Pricing | Negotiating.Pricing | new Episode |
| RollPricingDate | Negotiating.PendingPresentation | Negotiating.Pricing | new Episode; FirmQuote not carried |
| RollPricingDate | Negotiating.Presented | Negotiating.Pricing | current Presentation becomes latest; no Away fabricated |
| ChangeAssumedTradeDate | Inquiry.Pricing | Inquiry.Pricing | new Episode |
| ChangeAssumedTradeDate | Inquiry.PendingPresentation | Inquiry.Pricing | new Episode; FirmQuote not carried |
| ChangeAssumedTradeDate | Negotiating.Pricing | Negotiating.Pricing | new Episode |
| ChangeAssumedTradeDate | Negotiating.PendingPresentation | Negotiating.Pricing | new Episode; FirmQuote not carried |
| ChangeAssumedTradeDate | Negotiating.Presented | Negotiating.Pricing | current Presentation becomes latest; no Away fabricated |
| PresentQuote | Inquiry.PendingPresentation | Negotiating.Presented | first Presentation |
| PresentQuote | Negotiating.PendingPresentation | Negotiating.Presented | new current Presentation |
| ExtendValidity | Inquiry.PendingPresentation | Inquiry.PendingPresentation | preserve QuoteId |
| ExtendValidity | Negotiating.PendingPresentation | Negotiating.PendingPresentation | preserve QuoteId |
| ExtendValidity | Negotiating.Presented | Negotiating.Presented | preserve QuoteId and Presentation |
| RequestRepricingOnAway | Negotiating.Presented | Negotiating.Pricing | create Away + new Away-origin Episode |
| ChangeContactOwner | any Open | same Open state shape | change ContactOwnerId only |
| CloseCase(Unpresented) | Inquiry.Pricing / Inquiry.PendingPresentation | Terminal.Closed | no PresentationOutcome |
| CloseCase(Hit) | Negotiating.Presented | Terminal.Closed | create Hit outcome for current Presentation |
| CloseCase(Away.RecordNow) | Negotiating.Presented | Terminal.Closed | create Away outcome for current Presentation |
| CloseCase(Away.RecordNow) | Negotiating.Pricing / Negotiating.PendingPresentation | Terminal.Closed | create Away for latest Presentation when none recorded |
| CloseCase(Away.AlreadyRecorded) | Negotiating.Pricing / Negotiating.PendingPresentation | Terminal.Closed | reuse latest Presentation's existing Away |
| Cancel | any Open | Terminal.Cancelled | capture TerminalPricingContext; no ordinary Presentation outcome fabricated |
| ReopenAway | Terminal.Closed(Presented(Away)) | Negotiating.Pricing | new Episode; preserve prior Presentation/Away; no FirmQuote |
| ReopenUnpresented | Terminal.Closed(Unpresented) | Inquiry.Pricing | new Episode; preserve close Feedback in Origin |
| ReopenCancellation | Terminal.Cancelled(Withdrawn), PriorPresentation=None | Inquiry.Pricing | new Episode; genuine withdrawal resumes |
| ReopenCancellation | Terminal.Cancelled(Withdrawn), PriorPresentation=Some | Negotiating.Pricing | new Episode; preserve prior Presentation context |

Within any row, normal cross-field invariants still apply. Closed(Hit) and Cancelled(CreatedInError) have no ordinary Reopen transition.

### 18.2 State-centric graph

The graph is a cognitive/reference view of the operation definitions above. Repeated edges with the same operation name are not distinct Domain operation types.

    PublishDraft
      -> Inquiry.Pricing

    Inquiry.Pricing
      --CommitQuote--------------> Inquiry.PendingPresentation
      --ChangeRfqTerms-----------> Inquiry.Pricing
      --ChangeQuoteOwner---------> Inquiry.Pricing
      --RollPricingDate----------> Inquiry.Pricing
      --ChangeAssumedTradeDate---> Inquiry.Pricing
      --CloseCase(Unpresented)---> Terminal.Closed
      --Cancel-------------------> Terminal.Cancelled

    Inquiry.PendingPresentation
      --ReplaceFirmQuote---------> Inquiry.PendingPresentation
      --InvalidateQuote----------> Inquiry.Pricing
      --RequestRepricing---------> Inquiry.Pricing
      --ChangeRfqTerms-----------> Inquiry.Pricing
      --RollPricingDate----------> Inquiry.Pricing
      --ChangeAssumedTradeDate---> Inquiry.Pricing
      --PresentQuote-------------> Negotiating.Presented
      --ExtendValidity-----------> Inquiry.PendingPresentation
      --CloseCase(Unpresented)---> Terminal.Closed
      --Cancel-------------------> Terminal.Cancelled

    Negotiating.Pricing
      --CommitQuote--------------> Negotiating.PendingPresentation
      --ChangeQuoteOwner---------> Negotiating.Pricing
      --RollPricingDate----------> Negotiating.Pricing
      --ChangeAssumedTradeDate---> Negotiating.Pricing
      --CloseCase(Away)----------> Terminal.Closed
      --Cancel-------------------> Terminal.Cancelled

    Negotiating.PendingPresentation
      --ReplaceFirmQuote---------> Negotiating.PendingPresentation
      --InvalidateQuote----------> Negotiating.Pricing
      --RequestRepricing---------> Negotiating.Pricing
      --PresentQuote-------------> Negotiating.Presented
      --ExtendValidity-----------> Negotiating.PendingPresentation
      --RollPricingDate----------> Negotiating.Pricing
      --ChangeAssumedTradeDate---> Negotiating.Pricing
      --CloseCase(Away)----------> Terminal.Closed
      --Cancel-------------------> Terminal.Cancelled

    Negotiating.Presented
      --ReplaceFirmQuote---------> Negotiating.PendingPresentation
      --InvalidateQuote----------> Negotiating.Pricing
      --RequestRepricingOnAway---> Negotiating.Pricing
      --RollPricingDate----------> Negotiating.Pricing
      --ChangeAssumedTradeDate---> Negotiating.Pricing
      --ExtendValidity-----------> Negotiating.Presented
      --CloseCase(Hit)-----------> Terminal.Closed
      --CloseCase(Away)----------> Terminal.Closed
      --Cancel-------------------> Terminal.Cancelled

    any Open
      --ChangeContactOwner-------> same Open state shape

    Terminal.Closed(Presented(Away))
      --ReopenAway---------------> Negotiating.Pricing

    Terminal.Closed(Unpresented)
      --ReopenUnpresented--------> Inquiry.Pricing

    Terminal.Cancelled(Withdrawn, PriorPresentation=None)
      --ReopenCancellation-------> Inquiry.Pricing

    Terminal.Cancelled(Withdrawn, PriorPresentation=Some)
      --ReopenCancellation-------> Negotiating.Pricing

## 19. PricingEpisode creation matrix

Operations that create a new PricingEpisode:

| Operation | New RfqTerms? | Origin |
| --- | --- | --- |
| PublishDraft / initial Case creation | yes, initial | Initial |
| ChangeRfqTerms | yes | RfqTermsChanged(previous Episode) |
| ChangeQuoteOwner | no | QuoteOwnerChanged(previous Episode) |
| RollPricingDate | no | PricingDateRolled(previous Episode) |
| ChangeAssumedTradeDate | no | AssumedTradeDateChanged(previous Episode) |
| RequestRepricing | no | RepricingRequested(previous Episode, QuoteId, Feedback?) |
| RequestRepricingOnAway | no | Away(previous Episode, PresentationAwayOutcome) |
| ReopenAway | no | ReopenedFromAway(previous Episode, PresentationAwayOutcome) |
| ReopenUnpresented | no | ReopenedFromUnpresented(previous Episode, Feedback?) |
| ReopenCancellation | no | ReopenedFromCancellation(previous Episode, PriorPresentation?) |

PublishDraft supplies the initial PricingDate and AssumedTradeDate for the new Case. RollPricingDate changes PricingDate and preserves AssumedTradeDate. ChangeAssumedTradeDate changes AssumedTradeDate and preserves PricingDate. Other Open-operation Episode creation preserves both date fields from the previous Episode.

Every Reopen sets PricingDate=ReopenDate and uses the explicitly supplied NewAssumedTradeDate.

CommitQuote, ReplaceFirmQuote, PresentQuote, InvalidateQuote, ExtendValidity, and ChangeContactOwner preserve the current Open Episode.

CloseCase and Cancel terminate while retaining their source PricingEpisode inside TerminalPricingContext. Reopen then creates a new Episode from that terminal pricing context.

## 20. Why some apparently similar operations remain distinct

### ReplaceFirmQuote versus InvalidateQuote + CommitQuote

Replacing a FirmQuote is one business operation.

Do not model it as an explicit Invalidate followed by Commit. Replacement does not imply that the customer rejected the old price or that the business separately decided to invalidate it.

From Presented, replacement immediately makes the new Quote the current FirmQuote and moves to PendingPresentation. The old Presentation remains historical/latest and the old Quote is no longer normally Hit-eligible.

### Expired versus withdrawn invalidation

Both remove the current FirmQuote through InvalidateQuote, but QuoteInvalidationReason preserves the business distinction:

- Expired means validity elapsed and requires InvalidatedAt >= ValidUntil;
- Withdrawn means the firm condition is explicitly withdrawn for another reason and has no expiry-time precondition.

Do not create a separate ExpireQuote operation merely because the trigger differs.

### RequestRepricing versus InvalidateQuote

RequestRepricing creates a new PricingEpisode because a new logical pricing round is being requested before presentation.

InvalidateQuote preserves the current PricingEpisode.

### RequestRepricingOnAway versus ordinary repricing

RequestRepricingOnAway both establishes the current Presentation's Away outcome and creates a new PricingEpisode whose Origin retains that immutable Away outcome.

Ordinary RequestRepricing applies to an unpresented current FirmQuote and creates no Presentation outcome.

Application-side work on a better price while the current Presented FirmQuote remains live does not change Domain state at all.

## 21. WorkingQuote and the one-current-FirmQuote rule

At most one FirmQuote is current in the Domain.

WorkingQuote is outside Domain.

Therefore:

- trader may work on another price while a current Presented FirmQuote remains live;
- the Domain remains Presented during that work;
- when the new working value is committed as firm, ReplaceFirmQuote moves Domain to PendingPresentation;
- before that commit, the old Presented FirmQuote remains the normal Hit candidate;
- if the old FirmQuote is explicitly withdrawn before a replacement is committed, Domain moves to Pricing.

This avoids a two-axis Domain state model while preserving the real workflow.


## 22. Date and action-time semantics summary

All local dates below are BusinessEntityLocalDate and are interpreted under RfqCase.BusinessEntity.

- OpenDate: Case initiation/open date.
- PricingDate: local date to which a PricingEpisode's pricing belongs.
- AssumedTradeDate: trade date assumed for the PricingEpisode when evaluating trade-date-dependent terms; it is not the eventual executed TradeDate.
- PresentationDate: local date on which the Quote was presented to the customer.
- HitDate: local date on which the customer and desk agreed.
- AwayDate: business-effective local date attributed to a specific Presentation's Away outcome.
- CloseDate: local date on which the Case itself was closed.
- CancellationDate: local date on which the Case was cancelled.
- ReopenDate: local date attributed to the resumed pricing round when a genuine terminal Case reopens.

PricingDate, AssumedTradeDate, PresentationDate, HitDate, AwayDate, CloseDate, CancellationDate, and ReopenDate are distinct business concepts. The current Domain does not require equality among them.

Timepoint fields describe absolute business-action or business-event time where that distinction matters:

- CommittedAt: Quote commitment action;
- PresentedAt: Presentation action;
- HitAt: customer agreement time used by the validity invariant;
- PresentationAwayOutcome.RecordedAt: internal recording/establishment of the Away outcome;
- InvalidatedAt / RequestedAt / ChangedAt / RolledAt / ExtendedAt: accepted Domain-operation action times;
- ClosedAt / CancelledAt: Case terminal actions;
- ReopenedAt: accepted Reopen action;
- TraceRecord.RecordedAt: creation/recording of that Trace representation.

These Timepoint values are Domain business facts, not persistence insertion timestamps.

A business-effective local date and an operation/action Timepoint may intentionally differ. In particular, AwayDate may be attributed to an earlier business day than the day on which the internal actor records the Away outcome.

For Hit, normal chronology requires HitDate not to precede PresentationDate and HitAt <= current ValidUntil. CloseDate remains separate.

## 23. Operational revision chronology, restore, and durable TraceRecord

### 23.1 RfqCaseRevision and RfqCaseHistory

Positive-flow state semantics remain on RfqCase, but accepted operational changes are represented chronologically by immutable RfqCaseRevision values.

    RfqCaseRevision
    - CaseId
    - Version : CaseVersionNumber
    - Case : RfqCase
    - Transition : RfqCaseTransition

Invariant:

    RfqCaseRevision.CaseId == RfqCaseRevision.Case.CaseId

CaseVersionNumber is a Domain value with successor semantics:

    CaseVersionNumber
    - Value
    - Next()

RfqCaseHistory denotes the retained chronology of RfqCaseRevision values for one CaseId. This is a Domain history concept, not a requirement to eagerly load one large in-memory collection.

The current operational state is the RfqCase contained in the current/latest revision.

### 23.2 Revision transition provenance

    RfqCaseTransition
    = Published(DraftId)
    | Applied(CaseOperation)
    | RestoredFrom(TargetVersion)

Published(DraftId) is used only for the initial revision created by PublishDraft:

    transition == Published(...)
    => Version == InitialVersion

Applied(CaseOperation) means an accepted ordinary fully resolved Domain Operation was applied to the immediately preceding revision.

For every accepted ordinary operation after publication:

    next.CaseId == current.CaseId
    next.Version == current.Version.Next()
    next.Transition == Applied(operation)

This includes ReopenOperation. Reopen is genuine positive-flow business activity and therefore uses Applied(Reopen(...)); it does not use RestoredFrom.

One accepted Domain Operation creates exactly one next revision. Rejected operations and pure UI/Application work do not create revisions. An Application composite that executes several accepted Domain operations therefore creates several revisions even if they are persisted in one transaction.

The retained CaseOperation is the same Domain value used by live positive-flow semantics. It preserves generated Case-local IDs, supplied actor/time/date values, and other exogenous inputs required for deterministic replay, while values deterministically derivable from the source RfqCase are not duplicated merely for replay.

RfqCaseTransition therefore retains replayable business-operation provenance without introducing a separate replay DTO or command language for ordinary Case semantics.

### 23.3 Operational Restore

Operational Restore means abandoning the currently effective operational path and returning to a previously valid Open Case state so ordinary positive-flow processing can continue.

Given:

    v10 = earlier Open revision
    ...
    v20 = current revision

Restore(v10) creates:

    v21
    - CaseId = v20.CaseId
    - Version = v20.Version.Next()
    - Case = v10.Case
    - Transition = RestoredFrom(v10.Version)

Restore semantics:

- the current revision may be Open or Terminal;
- the target must be an existing earlier Open revision of the same Case chronology;
- terminal revisions are not Restore targets;
- the target may be an Open revision on a previously superseded path;
- v10 itself does not physically become current again;
- versions never move backward;
- the mistaken path remains immutable operational chronology;
- Case-local child identities from the target revision are reused exactly;
- later positive-flow operations create new Case-local identities normally;
- no explicit Branch/Worldline Domain object is currently required.

Restore is not a business activity in CaseActivityDigest. It changes which operational path is current.

Restore produces exactly one TraceRecord with Kind=RestoreSnapshot. Its Digest is the business-activity digest of the abandoned path that was current immediately before Restore. Digest.EndContext is therefore the abandoned path's endpoint, not the restored target's context.

Restore does not create a second TraceRecord for the restored Open target. The new current RfqCaseRevision already selects that state. Later durable milestones materialize new TraceRecords from the effective operational path.

A genuinely correct terminal occurrence followed by renewed activity is Reopen, not Restore.

### 23.4 Loading, persistence, and retention boundary

Ordinary Open operations normally require only the current RfqCaseRevision.

History-dependent revision-level operations require more:

- Restore requires current revision, target Open revision, and enough retained history to construct the RestoreSnapshot TraceRecord;
- TerminalOperation requires current revision, its TerminalOperation value, and TerminalTraceContext containing enough retained/resolved history plus Trace-recording context;
- ReopenOperation uses the current Terminal state's TerminalPricingContext to construct the new Open business state, but additionally requires ReopenTraceContext resolved from retained operational history to construct the Reopened TraceRecord.

This does not imply eager loading or whole-history rewrite. Implementation may query, index, stream, or materialize only the required historical facts.

A Reopened TraceRecord is materialized from retained operational history at Reopen time. It is not copied from an earlier Close/Cancel TraceRecord. The earlier record and the new Reopened record are independently materialized durable evidence; if their resolved representations differ, that discrepancy remains observable.

There is no fallback rule that switches Reopen Trace construction to an older TraceRecord when required operational history has already been removed. If retention no longer supports construction of ReopenTraceContext, Reopen is operationally unavailable.

Application/Persistence owns stale-write protection. A request observed against one current version commits only if that revision is still current.

A Domain operation that returns both a new RfqCaseRevision and TraceRecord has those facts persisted atomically.

Restore does not reverse external side effects such as already-sent customer communications, downstream notifications, or booking/integration effects. Such reconciliation belongs to Application/integration workflows.

Operational RfqCaseHistory may have finite retention and may eventually be deleted.

Retention must preserve enough operational history to execute every still-supported history-dependent Domain operation. In particular:

- while a Case is Open, retain enough history to construct a later terminal/Restore TraceRecord;
- after a genuine terminal occurrence, retain enough history for the supported Reopen/Restore window;
- after Reopen, do not discard the pre-Reopen chronology merely because a Reopened TraceRecord now exists; while the Case is Open, retain enough combined chronology for the next terminal/Restore TraceRecord;
- only after no supported operation still depends on the source chronology may retention remove it.

The Reopen support window is a Persistence/operational retention policy, not a Domain date invariant.

### 23.5 TraceRecord and CaseActivityDigest

TraceRecord is a durable materialization of a business-semantic Case activity digest. It is intentionally smaller than full RfqCaseHistory and may outlive the operational chronology from which it was constructed.

    TraceRecord
    - RecordedBy : ActorId
    - RecordedAt : Timepoint
    - RecordedBusinessDate : BusinessEntityLocalDate
    - Kind : TraceRecordKind
    - Digest : CaseActivityDigest

    TraceRecordKind
    = HitClose
    | AwayClose
    | UnpresentedClose
    | Cancelled
    | Reopened
    | RestoreSnapshot

    CaseActivityDigest
    - CaseId
    - BusinessEntity
    - OpenDate : BusinessEntityLocalDate
    - InitialContext : CaseContext
    - EndContext : CaseContext
    - Activities : TraceActivity[]

    CaseContext
    - ContactOwnerId
    - QuoteOwnerId
    - Terms : TraceTerms

RecordedBy / RecordedAt / RecordedBusinessDate describe production of this TraceRecord. They are distinct from action/event actor/time values inside the Digest.

InitialContext is the Case context at publication. EndContext is the context at the endpoint of the operational path represented by this Digest.

For RestoreSnapshot, EndContext is the abandoned current path immediately before Restore.

CaseActivityDigest is self-contained in the sense that its represented business activities and endpoint contexts can be interpreted after RfqCaseHistory is removed. It is not a promise to reproduce every intermediate operational state or every context value at every historical milestone. Earlier milestone TraceRecords remain independently durable.

TraceRecordKind states why this durable record exists:

- HitClose / AwayClose / UnpresentedClose correspond to the normal Case close outcome that triggered the record;
- Cancelled corresponds to Case cancellation;
- Reopened corresponds to genuine resumed business after a reopenable terminal occurrence;
- RestoreSnapshot preserves the abandoned operational path selected away by Operational Restore.

Normal record consistency requires HitClose / AwayClose / UnpresentedClose / Cancelled / Reopened records to end in the corresponding TraceActivity. RestoreSnapshot is a snapshot kind and is not required to end in a particular activity variant.

No separate TraceId is currently required. Persistence may add storage identity without turning it into Domain identity unless a later business rule requires it.

### 23.6 Durable Trace activities

Trace terms remove Case-local RfqTermsId while retaining durable business values:

    TraceTerms
    - ClientId
    - Side
    - SecurityId
    - Notional : NotionalAmount
    - SettlementDateRule

    PresentationPricingContext
    - QuoteOwnerId
    - PricingDate
    - AssumedTradeDate

    TraceQuote
    - Value
    - CommittedBy : ActorId
    - CommittedAt : Timepoint
    - EffectiveValidUntil : Timepoint

    TracePresentation
    - PresentationDate
    - PresentedBy : ActorId
    - PresentedAt : Timepoint

    FirstPresentationActivity
    - Terms : TraceTerms
    - ContactOwnerId
    - PricingContext : PresentationPricingContext
    - Quote : TraceQuote
    - Presentation : TracePresentation

    PresentationActivity
    - PricingContext : PresentationPricingContext
    - Quote : TraceQuote
    - Presentation : TracePresentation

    TraceHit
    - HitDate
    - HitAt

    TraceAway
    - AwayDate
    - RecordedBy : ActorId
    - RecordedAt : Timepoint
    - Feedback?

    ReopenActivity
    - ReopenDate : BusinessEntityLocalDate
    - ReopenedBy : ActorId
    - ReopenedAt : Timepoint

    TraceActivity
    = FirstPresentation(FirstPresentationActivity)
    | Presentation(PresentationActivity)
    | RepricingRequested(
          RejectedQuoteValue,
          Feedback?,
          RequestedBy,
          RequestedAt
      )
    | RepricingRequestedOnAway(
          PreviousQuoteValue,
          AwayDate,
          Feedback?,
          RequestedBy,
          RequestedAt
      )
    | QuoteOwnerChanged(
          PreviousQuoteOwnerId,
          ChangedBy,
          ChangedAt
      )
    | PricingDateRolled(
          PreviousPricingDate,
          RolledBy,
          RolledAt
      )
    | AssumedTradeDateChanged(
          PreviousAssumedTradeDate,
          ChangedBy,
          ChangedAt
      )
    | ValidityExtended(
          PreviousValidUntil,
          NewValidUntil,
          ExtendedBy,
          ExtendedAt
      )
    | HitClose(
          Close : CaseClose,
          Hit : TraceHit
      )
    | AwayClose(
          Close : CaseClose,
          Away : TraceAway
      )
    | UnpresentedClose(
          Close : CaseClose,
          Feedback?
      )
    | Cancelled(Cancellation)
    | Reopened(ReopenActivity)

TraceActivity is an ordered business-semantic digest, not a copy of CaseOperation or a full event log.

FirstPresentation is distinct because it is the Inquiry -> Negotiating boundary. It fixes the durable Terms and ContactOwner context under which customer negotiation first began. Same-Case Notional / SettlementDateRule changes are not allowed after this boundary, so a separate artificial FixTerms activity is unnecessary.

PresentationPricingContext records the QuoteOwner/PricingDate/AssumedTradeDate associated with the represented customer Presentation. Subsequent Presentations do not repeat Terms or ContactOwner merely because FirstPresentation did.

Terminal activity variants do not duplicate CaseContext. The context at a prior terminal milestone remains available in the durable TraceRecord materialized at that milestone; a later Digest retains the terminal activity occurrence without copying all of that earlier endpoint context.

Reopened is a genuine business activity. Restore is not and therefore has no TraceActivity variant.

### 23.7 Intentional Trace compression

TraceRecord deliberately does not preserve complete pre-Presentation or operational workflow history.

Before the first Presentation:

- ordinary pricing/context changes are compressed rather than retained as TraceActivity;
- this remains true even after a pre-Presentation Cancel/Reopen cycle;
- FirstPresentation carries the Terms, ContactOwner, and PresentationPricingContext at the boundary where negotiation begins.

The following lifecycle milestones are retained even if they occur before any Presentation:

- UnpresentedClose;
- Cancelled;
- Reopened.

HitClose and AwayClose inherently require a Presentation and therefore cannot occur before FirstPresentation.

After FirstPresentation:

- RepricingRequested is retained because an unpresented rejected FirmQuote value could otherwise disappear from the durable activity record;
- RepricingRequestedOnAway retains the transition from an Away customer proposal into a new pricing round;
- QuoteOwnerChanged, PricingDateRolled, AssumedTradeDateChanged, and ValidityExtended are retained with actor/time;
- ReplaceFirmQuote and InvalidateQuote remain compressed in the current Trace model;
- Close, Cancel, and Reopen milestones are retained as explicit TraceActivity variants.

TraceQuote.EffectiveValidUntil is materialized from operational chronology.

Two distinct customer Presentations remain distinct activities even if their numerical Quote values are equal.

These are deliberate information-compression choices, not claims that omitted operations are semantically equivalent.

### 23.8 History resolution and identity removal

CaseActivityDigest exposes no Case-local RfqTermsId, PricingEpisodeId, QuoteId, or PresentationId.

Construction resolves those identities to durable business values while operational history is available. The implementation may use temporary maps, joins, repository indexes, streaming, or another resolved-history representation.

For immutable Case-local entities, repeated use of the same ID across revisions must resolve to the same value.

Some Trace facts are chronological rather than simple ID lookups. EffectiveValidUntil and the ordered TraceActivity sequence, for example, depend on the effective operational path.

Because TraceRecord may outlive RfqCaseHistory, interpretation of its Digest must not require the retained revision chronology.

Historical correction may later replay/correct Applied(CaseOperation) transitions while operational history remains available. Exact corrected replay path/source selection and correction-output record semantics remain intentionally deferred.

### 23.9 Revision + TraceRecord results

CloseCase, Cancel, Reopen, and Operational Restore all create a durable TraceRecord at the revision/API boundary.

Conceptually:

    RevisionTraceResult
    - Revision : RfqCaseRevision
    - Trace : TraceRecord

    ApplyTerminal(
        currentRevision,
        operation : TerminalOperation,
        terminalTraceContext
    ) -> RevisionTraceResult

    ApplyReopen(
        currentRevision,
        operation : ReopenOperation,
        reopenTraceContext
    ) -> RevisionTraceResult

    Restore(
        currentRevision,
        targetRevision,
        restoreTraceContext
    ) -> RevisionTraceResult

Persistence appends the Domain-produced facts. The Domain determines Version.Next(), transition provenance, resulting Case content, TraceRecord kind, and CaseActivityDigest meaning; persistence does not reconstruct those semantics after the fact.

The Domain API preserves atomic semantic production of Revision + TraceRecord even if internal transition kernels and Trace materializers are factored separately.

## 24. Intentional negative decisions

The following earlier ideas were considered and rejected or deferred. Do not restore them casually.

### Requested / Unassigned RfqCase state

Rejected for the current RfqCase model.

RfqCase enters Domain only after QuoteOwner is known. Pre-publication work belongs to the separate RfqDraft aggregate and its Application workflows.

### Draft as RfqCase state

Rejected.

RfqDraft is a separate Aggregate Root with its own lifecycle and operations defined below. Do not reintroduce Draft into RfqCase.State.

### Repricing Domain state

Rejected for current scope.

Mutable WorkingQuote/repricing work is Application/UI state until it changes a Domain fact.

### Independent Presentation object was previously removed, then restored

Presentation is now required as QuotePresentation because PresentationDate is a real business fact and Presentation is the proper identity target for Hit/Away outcomes.

### Quote outcome directly on Quote

Rejected.

A Quote can be presented as a distinct business event. Outcomes apply to QuotePresentation.

### OutcomeId

Rejected.

PresentationId is sufficient identity for one effective Presentation outcome.

### Independent Presentation Hit/Away terminal hierarchy plus Case-close wrappers

Simplified into:

- PresentationOutcome;
- CaseOutcome;
- CaseClose;
- TerminalState.

CaseClose is retained as the shared close-action value, while PresentationOutcome and CaseOutcome preserve the two levels of outcome meaning without a separate hierarchy of terminal wrappers.

### Orthogonal ClientState x QuoteWorkState

Rejected for current scope.

One current FirmQuote plus Application-side WorkingQuote is enough.

### Initial PricingDate equals OpenDate as a hard invariant

Not adopted.

Application normally supplies equal dates, but Domain does not currently reject a difference.

## 25. RfqDraft

RfqDraft is a separate Aggregate Root within the RFQ bounded context. It represents pre-publication business work used to assemble a customer RFQ before that RFQ is formally issued into the RfqCase workflow.

Draft creation is not itself formal RFQ issuance. PublishDraft is the business boundary at which the customer request enters the formal RFQ flow and a valid RfqCase is created.

RfqDraft is intentionally more permissive than RfqCase: required publication fields may still be undetermined, and cross-field combinations that would not form a valid RfqCase may exist while the Draft is being edited. Publication, not Draft editing, is the boundary at which complete RfqCase validity is required.

### 25.1 DraftField and RfqDraftData

A field required by publication but not yet decided is represented explicitly:

    DraftField<T>
    = Undetermined
    | Determined(T)

Undetermined means "not yet determined." It does not mean that the business field is permanently optional or not applicable.

Determined(T) assumes T is already a valid value of its own Domain type. RfqDraft does not duplicate local validation owned by T.

RfqDraftData represents the RFQ content being assembled before publication:

    RfqDraftData
    - ClientId : DraftField<ClientId>
    - Side : DraftField<Side>
    - SecurityId : DraftField<SecurityId>
    - Notional : DraftField<NotionalAmount>
    - SettlementDateRule : DraftField<SettlementDateRule>
    - AssumedTradeDate : DraftField<BusinessEntityLocalDate>

AssumedTradeDate is included in RfqDraftData because it is part of the RFQ input being established before publication. It is not an RfqTerms field: after publication it becomes a PricingEpisode assumption used when evaluating trade-date-dependent terms such as TradeDateLag settlement.

ContactOwnerId and QuoteOwnerId are not part of RfqDraftData. They represent distinct responsibility assignments rather than RFQ content, and they have their own Domain operations.

A future field that is genuinely optional even for publication should be modeled as such rather than overloading Undetermined.

RfqDraft itself is:

    RfqDraft
    - DraftId
    - BusinessEntity
    - DraftOwnerId
    - Data : RfqDraftData
    - ContactOwnerId : DraftField<ContactOwnerId>
    - QuoteOwnerId : DraftField<QuoteOwnerId>
    - State : RfqDraftState

Semantics:

- BusinessEntity is required and immutable for the Draft lifetime;
- DraftOwnerId is required and may change only while Active;
- Data, ContactOwnerId, and QuoteOwnerId are retained in every Draft state;
- Data may change only while Active through AmendDraftData;
- ContactOwnerId and QuoteOwnerId may change only while Active through their dedicated operations;
- DraftOwner is responsibility for the pre-publication Draft and is not copied into RfqCase on publication;
- ContactOwner is customer-facing responsibility for the RFQ;
- QuoteOwner is pricing responsibility for the RFQ.

### 25.2 RfqDraft lifecycle

    RfqDraftState
    = Active
    | Deleted
    | Published(CaseId)

Active is the editable pre-publication state.

Deleted is a logical deletion, not physical removal. Draft content and responsibility assignments are retained so the Draft can be restored or copied.

Published means the Draft has already created the identified Case chronology. Published is terminal for that Draft. Its Data, ContactOwnerId, and QuoteOwnerId remain as the publication-time Draft snapshot and may be used as the source of CopyDraft; subsequent business state is carried by the Case's RfqCaseRevision chronology.

### 25.3 RfqDraft operations

Domain operations are defined by their semantic effect; this notation does not require a member-function implementation.

    [D01] CreateDraft
          -> Active

    Active
      --[D02 AmendDraftData]-----------> Active
      --[D03 ChangeDraftOwner]---------> Active
      --[D04 ChangeDraftContactOwner]--> Active
      --[D05 ChangeDraftQuoteOwner]----> Active
      --[D06 DeleteDraft]--------------> Deleted
      --[D10 PublishDraft]-------------> Published(CaseId)

    Deleted
      --[D07 RestoreDraft]-------------> Active

CopyDraft does not transition its source:

    [D08] CopyDraft
          Active | Deleted | Published
          -> new Active RfqDraft

SeedDraftFromCase does not transition its source Case:

    [D09] SeedDraftFromCase
          any RfqCase state
          -> new Active RfqDraft

Operation semantics:

| No. | Operation | Source | Main semantic effect |
| --- | --- | --- | --- |
| D01 | CreateDraft | none | Create Active Draft from given DraftId, BusinessEntity, DraftOwnerId, RfqDraftData, ContactOwnerId field, and QuoteOwnerId field |
| D02 | AmendDraftData | Active | Replace Data with a new RfqDraftData; Data fields may move between Undetermined and Determined |
| D03 | ChangeDraftOwner | Active | Change DraftOwnerId without changing RFQ content, responsibility assignments, or lifecycle state |
| D04 | ChangeDraftContactOwner | Active | Change ContactOwnerId between Undetermined and Determined states without changing Data |
| D05 | ChangeDraftQuoteOwner | Active | Change QuoteOwnerId between Undetermined and Determined states without changing Data |
| D06 | DeleteDraft | Active | Logically delete the Draft while retaining content and responsibility assignments |
| D07 | RestoreDraft | Deleted | Restore the same Draft and retained fields to Active |
| D08 | CopyDraft | any Draft state | Create a new Active Draft using field-specific copy/reset rules; source is unchanged |
| D09 | SeedDraftFromCase | any RfqCase state | Create a new Active Draft from explicitly reusable current Case facts; source is unchanged |
| D10 | PublishDraft | Active | Publish the Draft and create the initial RfqCaseRevision containing a valid new RfqCase as one Domain operation |

Deleted permits no content amendment, responsibility change, owner change, or publish operation. Published is terminal and permits no operation on that Draft other than acting as a CopyDraft source.

The dedicated ContactOwner/QuoteOwner operations express different Domain meanings from changing RFQ content. They do not imply actor-specific authorization rules. Actor authorization, edit delegation, audit actor/time, ID allocation, external routing, optimistic concurrency, and persistence transaction management remain Application/Persistence concerns.

### 25.4 CreateDraft and AmendDraftData

CreateDraft receives DraftId, BusinessEntity, DraftOwnerId, an RfqDraftData, a DraftField<ContactOwnerId>, and a DraftField<QuoteOwnerId>. Any DraftField may initially be Undetermined or Determined. CreateDraft does not require publication completeness or RfqCase cross-field consistency.

AmendDraftData receives a replacement RfqDraftData for an Active Draft. It may change multiple RFQ-content fields atomically and may return previously Determined Data fields to Undetermined. It does not change DraftOwnerId, ContactOwnerId, or QuoteOwnerId.

This permissiveness is intentional. RfqDraft represents pre-publication work; it is not a partially valid RfqCase.

### 25.5 Draft responsibility changes

ChangeDraftOwner changes responsibility for managing the Draft itself.

ChangeDraftContactOwner changes the customer-facing responsibility assignment:

    DraftField<ContactOwnerId>
    = Undetermined
    | Determined(ContactOwnerId)

ChangeDraftQuoteOwner changes the pricing responsibility assignment:

    DraftField<QuoteOwnerId>
    = Undetermined
    | Determined(QuoteOwnerId)

Both ContactOwnerId and QuoteOwnerId may therefore move:

- Undetermined -> Determined;
- Determined -> another Determined value;
- Determined -> Undetermined.

No SecurityId-to-QuoteOwner eligibility invariant is currently imposed by Domain. Automatic QuoteOwner routing, clearing/re-routing after SecurityId changes, and authorization for manual responsibility assignment are Application concerns.

### 25.6 DeleteDraft and RestoreDraft

DeleteDraft is a reversible logical deletion:

    Active -> Deleted

RestoreDraft restores the same Draft:

    Deleted -> Active

Both operations preserve DraftId, BusinessEntity, DraftOwnerId, RfqDraftData, ContactOwnerId, and QuoteOwnerId.

No DeletionReason is currently modeled. Actor, reason, and timestamp may be retained as audit/Application facts unless a concrete requirement makes them Domain-significant.

### 25.7 CopyDraft

CopyDraft may use an Active, Deleted, or Published Draft as its source. The source is unchanged.

The new Draft:

- receives a new DraftId;
- receives a supplied DraftOwnerId;
- preserves BusinessEntity;
- is Active;
- copies ClientId, Side, SecurityId, Notional, and SettlementDateRule exactly as DraftField values from source Data;
- sets Data.AssumedTradeDate to Undetermined;
- sets ContactOwnerId to Undetermined;
- sets QuoteOwnerId to Undetermined.

AssumedTradeDate and responsibility assignments are deliberately re-established for the new RFQ rather than inherited.

Copy semantics are field-specific. When a new RfqDraftData or root-level DraftField is added, its copy/reset behavior must be decided explicitly rather than implicitly copying every future field.

### 25.8 SeedDraftFromCase

SeedDraftFromCase creates a new Active Draft from any RfqCase state.

It is not a complete reverse mapping from RfqCase to RfqDraft. It seeds only facts that remain semantically reusable for a new RFQ.

Current mapping:

- BusinessEntity <- source RfqCase.BusinessEntity;
- Data.ClientId <- Determined(source current RfqTerms.ClientId);
- Data.Side <- Determined(source current RfqTerms.Side);
- Data.SecurityId <- Determined(source current RfqTerms.SecurityId);
- Data.Notional <- Determined(source current RfqTerms.Notional);
- Data.SettlementDateRule <- Determined(source current RfqTerms.SettlementDateRule);
- Data.AssumedTradeDate <- Undetermined;
- DraftOwnerId <- supplied for the new Draft;
- ContactOwnerId <- Undetermined;
- QuoteOwnerId <- Undetermined;
- State <- Active.

PricingDate is not a Draft field and is not seeded.

Future Draft fields are not assumed to be reconstructible from every RfqCase state. A new field must explicitly define whether SeedDraftFromCase seeds it from an available Case fact or leaves it Undetermined.

### 25.9 PublishDraft

PublishDraft is allowed only from Active.

Publication requires every RfqDraftData field modeled as DraftField<T>, ContactOwnerId, and QuoteOwnerId to be Determined.

PublishDraft receives or is supplied externally with the identities and dates required to construct the Case, including CaseId, the initial Case-local RfqTermsId and PricingEpisodeId, OpenDate, and PricingDate. ID allocation and supplying BusinessEntityLocalDate values remain Application/external-context responsibilities.

The generated RfqCase mapping is:

    RfqDraft.BusinessEntity
        -> RfqCase.BusinessEntity

    RfqDraft.ContactOwnerId
        -> RfqCase.ContactOwnerId

    RfqDraft.Data.{
        ClientId,
        Side,
        SecurityId,
        Notional,
        SettlementDateRule
    }
        -> initial RfqTerms

    RfqDraft.QuoteOwnerId
    RfqDraft.Data.AssumedTradeDate
    supplied PricingDate
        -> initial PricingEpisode

    supplied OpenDate
        -> RfqCase.OpenDate

The initial PricingEpisode has Origin=Initial and the new Case begins in Inquiry.Pricing.

DraftOwnerId is not carried into RfqCase.

No equality invariant is introduced between OpenDate, PricingDate, or AssumedTradeDate.

The resulting RfqCase must satisfy every normal RfqCase invariant. In particular, if SettlementDateRule is ExplicitDate(SettlementDate), publication requires:

    SettlementDate >= AssumedTradeDate

An Active Draft may temporarily contain Determined values that fail this cross-field Case invariant; such a Draft is simply not publishable until amended.

PublishDraft has one indivisible Domain meaning: the source Draft becomes Published(CaseId) and the initial RfqCaseRevision is created with Version=InitialVersion and Transition=Published(DraftId). Its contained RfqCase is the valid Inquiry.Pricing state described above. Persisting both effects atomically, ensuring single publication, and rejecting publication based on a stale observed Draft version are Application/Persistence responsibilities.

### 25.10 Draft provenance and lineage

CopyDraft and SeedDraftFromCase do not currently add SourceDraftId, SourceCaseId, or generic lineage fields to RfqDraft or RfqCase.

Application/audit persistence may record source-to-derived relationships so provenance is not lost. If future business rules, statistics, or workflow invariants depend on lineage, introduce a concrete Domain concept at that time rather than pre-generalizing the aggregates.

## 26. Deferred domain topics

The following topics are deliberately not completed in this document.


### 26.1 Correction / reversal / historical amendment

Operational Restore, RfqCaseRevision chronology, CaseOperation provenance, Reopen, and durable TraceRecord / CaseActivityDigest are now canonical.

Broader historical correction remains intentionally incomplete.

The current foundation allows historical-correction design to start from concrete retained material:

    initial Published revision
      + Applied(CaseOperation) chronology
      + RestoredFrom provenance
      + durable TraceRecord milestones

Ordinary CaseOperation replay/correction may be reused while the intended corrected history remains expressible by the positive-flow RfqCase model. A correction-native representation may still be required for business histories that cannot be represented by ordinary RfqCase states/operations.

The exact replay source/path selection, allowed edits to the operation program, correction-side commands/state, correction-of-correction, and output-record semantics remain deferred to the active historical-correction design.

Correction must preserve auditability and must not become a generic mutation escape hatch. External/irreversible side effects remain distinct from historical business representation.

### 26.2 CancellationReason taxonomy

The current positive-flow taxonomy is intentionally minimal:

    CancellationReason
    = Withdrawn
    | CreatedInError

Withdrawn is the only ordinary cancellation reason eligible for Reopen. CreatedInError includes duplicate/mistaken creation cases and is not reopenable through ordinary positive flow.

Future business/statistical requirements may refine or extend the non-reopenable reason taxonomy. Such extension should be driven by concrete operational/reporting needs rather than speculative enumeration.

### 26.3 Presented Case close disposition

No separate Case-level Presented-Away close classification is modeled yet.

Future analytics may require typed dispositions such as ClientWithdrew, NoResponse, PriceRejected, TradedElsewhere, or operational late-close reason.

### 26.4 Case and Draft lineage

Current CopyDraft/SeedDraftFromCase provenance is intentionally kept outside the aggregates. Whether future Case/Draft lineage becomes a first-class Domain concept remains deferred until a concrete business rule depends on it.

### 26.5 Application use cases and authorization

To be designed after the positive Domain model.

Known examples include:

- Contact Owner handoff/takeover/management reassign;
- Quote Owner handoff;
- composites that first return a Case to Pricing then change QuoteOwner;
- current local-date roll + commit;
- Draft publish/copy workflows.

The Domain operations must remain actor-neutral unless actor identity itself becomes a business invariant.

### 26.6 Foreign-market settlement semantics

Current explicit settlement dates are BusinessEntityLocalDate, and TradeDateLag resolves from PricingEpisode.AssumedTradeDate to BusinessEntityLocalDate.

This is an intentional current-scope simplification based on the workflows currently modeled. If foreign settlement requires a distinct market-local date context, extend the model rather than weakening the meaning of BusinessEntityLocalDate.

### 26.7 Settlement amount

Settlement amount/currency/FX-conversion rule is a future orthogonal enhancement.

Example: foreign-currency security settled operationally in JPY.

Do not force this into SettlementDateRule.

## 27. Guidance for Codex / future implementation work

Before changing the RFQ Domain implementation:

1. read this document first;
2. treat current code types such as old Draft/Revision/Requested/Confirmed state models as migration input, not authority;
3. do not preserve an old abstraction solely because it exists in persistence or API;
4. keep Application authorization separate from Domain state validity;
5. do not introduce generic setters/update methods to make migration easier;
6. implement typed RfqCase states and first-class CaseOperation semantics from Section 17, using the applicability table/state graph as the valid source-target view;
7. wrap accepted Case operations in RfqCaseRevision chronology with Domain-declared Version.Next() and RfqCaseTransition provenance;
8. preserve explicit Case-local child identity, the separate DraftId identity, and CaseActivityDigest's deliberate removal of Case-local references;
9. implement RfqDraft as a separate pre-publication model with DraftField/RfqDraftData semantics rather than reviving the old Draft-as-RfqCase-state model;
10. do not interpret RfqCaseHistory as a requirement to eagerly load or rewrite the complete history for ordinary operations;
11. persist revision + TraceRecord results atomically when one Domain operation produces both;
12. add tests around positive-flow invariants, revision invariants, restore targets, and Trace materialization before broad API/UI rewiring;
13. if a new requirement conflicts with this model, document the business requirement and revisit the model rather than silently adding a bypass.
