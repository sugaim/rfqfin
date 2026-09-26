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
- retained operational chronology from durable TraceData that may outlive that chronology;
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

Conversely, two operations may share the same source and target state but remain different Domain operations when their business meaning differs. In particular, ExpireQuote and InvalidateQuote are not collapsed merely because both remove the current FirmQuote; expiry and an explicit business withdrawal are different facts.

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

Operational correction is no longer wholly Domain-external: operational Restore, RfqCaseRevision chronology, and durable TraceData are defined below.

The broader historical-correction command language remains deliberately deferred. In particular, the exact replayable CaseCommand representation and the Trace-native EffectiveCommand model are not yet canonicalized beyond the minimum chronology semantics required here.

## 4. Naming and notation

The domain documentation uses C#-compatible PascalCase for type names, field names, and operation names. This is intentional so that the conceptual model maps cleanly to the implementation.

Use:

- RfqCase, RfqDraft, RfqTerms, PricingEpisode;
- CaseId, DraftId, QuoteId, PresentationId;
- CommitQuote, PresentQuote, ContinueAfterAway, PublishDraft.

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

TraceData also has no separate TraceId in the current Domain model. Persistence may add storage keys or sequence metadata without turning them into Domain identity unless later business rules require such identity.

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
- TraceData.RecordedAt.

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
    | ContinuedAfterAway(
          PreviousPricingEpisodeId,
          PresentationAwayOutcome
      )

Origin is exactly one variant, not a bag/list of causes.

If an Application composite performs two episode-changing Domain operations, two episodes are created in sequence. The current episode is the latest one.

Origin stores only information needed to explain why the new pricing round exists. Most variants identify the previous PricingEpisode so the Episode lineage remains explicit. ContinuedAfterAway additionally retains the immutable PresentationAwayOutcome that caused continuation; this intentional value duplication keeps the Episode's business provenance stable even after the Case moves on to later Presentations.

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

FirmQuote is current-state data meaning that the Quote is currently firm.

At most one FirmQuote is current in a Case.

ValidUntil is not part of Quote identity. A still-valid FirmQuote may have ValidUntil extended while retaining the same QuoteId.

An expired quote is not revived by extending ValidUntil. A new Quote is required after expiry.

Time passing does not automatically mutate Domain state. ExpireQuote is an explicit operation.

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
    - Feedback : string?

AwayDate is the date on which that Presentation became known/recorded as Away.

Away is not necessarily terminal for the Case. It may create a new PricingEpisode through ContinueAfterAway.

AwayDate is not required to equal PresentationDate or PricingDate.

## 13. Case outcome and terminal state

The outcome of a valid RFQ Case is modeled separately from cancellation.

    CaseOutcome
    = Presented(PresentationOutcome)
    | Unpresented(Feedback?)

    TerminalState
    = Closed(
          CloseDate : BusinessEntityLocalDate,
          ClosedBy : ActorId,
          ClosedAt : Timepoint,
          Outcome : CaseOutcome
      )
    | Cancelled(
          CancellationDate : BusinessEntityLocalDate,
          CancelledBy : ActorId,
          CancelledAt : Timepoint,
          CancellationReason
      )

Semantics:

- Closed means a valid RFQ Case reached a normal business conclusion;
- Cancelled means the Case should not be treated as an ordinary Hit/Away outcome, e.g. invalid, duplicate, or created in error;
- exact CancellationReason variants are intentionally deferred until exception/correction requirements are designed;
- CancellationReason is expected to be typed, not an unrestricted string.

CloseDate is the BusinessEntity-local date the Case itself was closed. ClosedAt is the absolute time of the closing action and ClosedBy is the actual actor that performed it. These are distinct from HitDate and AwayDate.

CancellationDate is likewise the BusinessEntity-local cancellation date, while CancelledAt and CancelledBy record the absolute action time and actor.

Examples:

- a customer may Hit on one date and operational Case closure may be recorded later;
- a Presentation may go Away, pricing may continue, and the Case may be closed on a later date.

### 13.1 Presented Case close and existing Away outcomes

When a Negotiating Case closes Away:

- use the latest QuotePresentation;
- if that Presentation already has a PresentationAwayOutcome, reuse it;
- otherwise create the PresentationAwayOutcome as part of the close operation;
- CaseOutcome is Presented(Away(...)).

This rule avoids manufacturing a second Away result for a Presentation that already went Away and triggered continued pricing.

### 13.2 Unpresented close

A Case that has never reached a customer Presentation closes as:

    CaseOutcome.Unpresented(Feedback?)

There is no PresentationOutcome because no Presentation exists.

### 13.3 Deferred close classification

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

Historical PricingEpisodes may refer to historical RfqTerms entities. The current state always refers to the current RfqTerms.

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

### 16.7 Close timing

CloseDate is separate from outcome date.

In normal flow, a Presented outcome must exist no later than Case CloseDate.

CancellationDate is the date of Case cancellation, not a Presentation outcome date.

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

This invariant applies to every Domain operation that can establish a new current Terms/Episode combination, including CreateCase, ChangeRfqTerms, ChangeAssumedTradeDate, and PublishDraft through the RfqCase it creates.

### 16.11 Time does not mutate state

Clock passage alone does not change Domain state.

An expired FirmQuote may remain structurally current until ExpireQuote or another explicit operation occurs.

Hit always checks HitAt against ValidUntil, so delayed expiry processing cannot allow an invalid Hit.

## 17. Transition graph

Every distinct Domain operation is numbered even when two operations share the same source and target.

### Inquiry transitions

    [T01] PublishDraft / initial Case creation
          -> Inquiry.Pricing

T01 is the Case-side effect of D10 PublishDraft. There is no separate normal RfqCase creation path in the current model.

    Inquiry.Pricing
      --[T02 CommitQuote]---------> Inquiry.PendingPresentation
      --[T07 ChangeRfqTerms]-----> Inquiry.Pricing
      --[T09 ChangeQuoteOwner]---> Inquiry.Pricing
      --[T10 RollPricingDate]----> Inquiry.Pricing
      --[T38 ChangeAssumedTradeDate]--> Inquiry.Pricing
      --[T31 CloseAway]----------> Terminal.Closed
      --[T36 Cancel]-------------> Terminal.Cancelled

    Inquiry.PendingPresentation
      --[T03 ReplaceFirmQuote]---> Inquiry.PendingPresentation
      --[T04 InvalidateQuote]----> Inquiry.Pricing
      --[T05 ExpireQuote]--------> Inquiry.Pricing
      --[T06 RequestRepricing]---> Inquiry.Pricing
      --[T08 ChangeRfqTerms]-----> Inquiry.Pricing
      --[T11 RollPricingDate]----> Inquiry.Pricing
      --[T39 ChangeAssumedTradeDate]--> Inquiry.Pricing
      --[T12 PresentQuote]-------> Negotiating.Presented
      --[T13 ExtendValidUntil]---> Inquiry.PendingPresentation
      --[T32 CloseAway]----------> Terminal.Closed
      --[T36 Cancel]-------------> Terminal.Cancelled

### Negotiating transitions

    Negotiating.Pricing
      --[T14 CommitQuote]--------> Negotiating.PendingPresentation
      --[T21 ChangeQuoteOwner]---> Negotiating.Pricing
      --[T22 RollPricingDate]----> Negotiating.Pricing
      --[T40 ChangeAssumedTradeDate]--> Negotiating.Pricing
      --[T33 CloseAway]----------> Terminal.Closed
      --[T36 Cancel]-------------> Terminal.Cancelled

    Negotiating.PendingPresentation
      --[T15 ReplaceFirmQuote]---> Negotiating.PendingPresentation
      --[T16 InvalidateQuote]----> Negotiating.Pricing
      --[T17 ExpireQuote]--------> Negotiating.Pricing
      --[T18 RequestRepricing]---> Negotiating.Pricing
      --[T19 PresentQuote]-------> Negotiating.Presented
      --[T20 ExtendValidUntil]---> Negotiating.PendingPresentation
      --[T23 RollPricingDate]----> Negotiating.Pricing
      --[T41 ChangeAssumedTradeDate]--> Negotiating.Pricing
      --[T34 CloseAway]----------> Terminal.Closed
      --[T36 Cancel]-------------> Terminal.Cancelled

    Negotiating.Presented
      --[T24 ReplaceFirmQuote]---> Negotiating.PendingPresentation
      --[T25 InvalidateQuote]----> Negotiating.Pricing
      --[T26 ExpireQuote]--------> Negotiating.Pricing
      --[T27 ContinueAfterAway]--> Negotiating.Pricing
      --[T28 RollPricingDate]----> Negotiating.Pricing
      --[T42 ChangeAssumedTradeDate]--> Negotiating.Pricing
      --[T29 ExtendValidUntil]---> Negotiating.Presented
      --[T30 Hit]----------------> Terminal.Closed
      --[T35 CloseAway]----------> Terminal.Closed
      --[T36 Cancel]-------------> Terminal.Cancelled

### Case-level operation independent of lifecycle subtype

    [T37] ChangeContactOwner
          Open -> same Open state shape

ChangeContactOwner changes ContactOwnerId but does not change PricingEpisode, Quote, Presentation, or lifecycle state.

## 18. Transition semantics table

| No. | Operation | Main semantic effect | PricingEpisode | Important invariants / generated facts |
| --- | --- | --- | --- | --- |
| T01 | PublishDraft / initial Case creation | Create the valid initial RfqCase in Inquiry.Pricing as the Case-side effect of D10 PublishDraft | new, Origin=Initial | initial RfqCaseRevision uses Published(DraftId); QuoteOwner already known |
| T02 | CommitQuote | Make a new Quote current and firm | preserve | Quote.PricingEpisodeId=current Episode |
| T03 | ReplaceFirmQuote | Replace unpresented current FirmQuote | preserve | old Quote becomes historical; no Away outcome |
| T04 | InvalidateQuote | Explicitly withdraw unpresented FirmQuote | preserve | no automatic outcome |
| T05 | ExpireQuote | Remove unpresented FirmQuote because validity has elapsed | preserve | semantically different from InvalidateQuote |
| T06 | RequestRepricing | Reject current unpresented firm price as the next pricing-round starting point | new, Origin=RepricingRequested | Origin references old QuoteId; optional Feedback; no Presentation outcome |
| T07 | ChangeRfqTerms | Change permitted Terms while Inquiry/Pricing | new, Origin=RfqTermsChanged | create new RfqTermsId; only Notional/SettlementDateRule may change |
| T08 | ChangeRfqTerms | Change permitted Terms while Inquiry/Pending | new, Origin=RfqTermsChanged | current FirmQuote is not carried forward |
| T09 | ChangeQuoteOwner | Change pricing responsibility in Inquiry.Pricing | new, Origin=QuoteOwnerChanged | only allowed when no current FirmQuote |
| T10 | RollPricingDate | Start pricing on later local date | new, Origin=PricingDateRolled | NewPricingDate > old; no FirmQuote carried |
| T11 | RollPricingDate | Roll date from Inquiry/Pending | new, Origin=PricingDateRolled | old unpresented FirmQuote is not current in new Episode |
| T12 | PresentQuote | First customer presentation | preserve | create QuotePresentation; phase becomes Negotiating |
| T13 | ExtendValidUntil | Extend current unpresented FirmQuote | preserve | same QuoteId; only before expiry; new validity later than old |
| T14 | CommitQuote | Create current firm price after prior presentation history | preserve | preserves LatestPresentation information |
| T15 | ReplaceFirmQuote | Replace current unpresented FirmQuote | preserve | prior latest customer Presentation remains unchanged |
| T16 | InvalidateQuote | Withdraw current unpresented FirmQuote | preserve | returns to Pricing; latest Presentation/outcome preserved |
| T17 | ExpireQuote | Remove expired current unpresented FirmQuote | preserve | latest Presentation/outcome preserved |
| T18 | RequestRepricing | Start new pricing round after rejecting unpresented firm price | new, Origin=RepricingRequested | latest prior Presentation/outcome preserved |
| T19 | PresentQuote | Present current FirmQuote after previous customer proposal(s) | preserve | create new QuotePresentation; becomes current Presentation |
| T20 | ExtendValidUntil | Extend current unpresented FirmQuote | preserve | same QuoteId |
| T21 | ChangeQuoteOwner | Change pricing responsibility in Negotiating.Pricing | new, Origin=QuoteOwnerChanged | latest Presentation/outcome preserved |
| T22 | RollPricingDate | Roll later while already Pricing | new, Origin=PricingDateRolled | latest Presentation/outcome preserved |
| T23 | RollPricingDate | Roll later from Pending | new, Origin=PricingDateRolled | FirmQuote removed; latest Presentation/outcome preserved |
| T24 | ReplaceFirmQuote | Commit a new firm price while a previous quote is Presented | preserve | current Presentation becomes LatestPresentation with no outcome; new FirmQuote is PendingPresentation |
| T25 | InvalidateQuote | Withdraw the currently Presented FirmQuote | preserve | current Presentation becomes LatestPresentation with no outcome |
| T26 | ExpireQuote | Presented FirmQuote expires | preserve | current Presentation becomes LatestPresentation with no outcome |
| T27 | ContinueAfterAway | Customer proposal goes Away but Case continues | new, Origin=ContinuedAfterAway | create PresentationAwayOutcome; retain that Away outcome in Origin; carry Presentation + Away outcome as latest |
| T28 | RollPricingDate | Start a later-date pricing round from Presented | new, Origin=PricingDateRolled | current Presentation becomes latest; no Away outcome is fabricated |
| T29 | ExtendValidUntil | Extend still-valid Presented FirmQuote | preserve | same Quote and same Presentation |
| T30 | Hit | Customer accepts current Presented FirmQuote | preserve/terminal | create PresentationHitOutcome; HitDate does not precede PresentationDate; HitAt<=ValidUntil |
| T31 | CloseAway | Close an Inquiry Case before any Presentation | terminal | CaseOutcome.Unpresented(Feedback?) |
| T32 | CloseAway | Close an Inquiry Case with unpresented FirmQuote | terminal | still CaseOutcome.Unpresented; current Quote gets no Presentation outcome |
| T33 | CloseAway | Close Negotiating.Pricing as Away | terminal | use LatestPresentation Away outcome if present; otherwise create it |
| T34 | CloseAway | Close Negotiating.PendingPresentation as Away | terminal | outcome applies to LatestPresentation, not current unpresented FirmQuote |
| T35 | CloseAway | Close current Presented proposal as Away | terminal | create PresentationAwayOutcome for current Presentation |
| T36 | Cancel | Mark Case invalid/non-normal outcome | terminal | no ordinary Hit/Away result is manufactured; typed reason details deferred |
| T37 | ChangeContactOwner | Reassign customer-contact ownership | preserve | state shape and PricingEpisode unchanged |
| T38 | ChangeAssumedTradeDate | Change trade-date assumption in Inquiry.Pricing | new, Origin=AssumedTradeDateChanged | PricingDate preserved; no FirmQuote to carry |
| T39 | ChangeAssumedTradeDate | Change trade-date assumption from Inquiry.Pending | new, Origin=AssumedTradeDateChanged | PricingDate preserved; current FirmQuote is not carried forward |
| T40 | ChangeAssumedTradeDate | Change trade-date assumption in Negotiating.Pricing | new, Origin=AssumedTradeDateChanged | PricingDate and latest Presentation/outcome preserved |
| T41 | ChangeAssumedTradeDate | Change trade-date assumption from Negotiating.Pending | new, Origin=AssumedTradeDateChanged | PricingDate preserved; FirmQuote removed; latest Presentation/outcome preserved |
| T42 | ChangeAssumedTradeDate | Change trade-date assumption from Presented | new, Origin=AssumedTradeDateChanged | PricingDate preserved; current Presentation becomes latest; no Away outcome is fabricated |

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
| ContinueAfterAway | no | ContinuedAfterAway(previous Episode, PresentationAwayOutcome) |

PublishDraft supplies the initial PricingDate and AssumedTradeDate for the new Case. RollPricingDate changes PricingDate and preserves AssumedTradeDate. ChangeAssumedTradeDate changes AssumedTradeDate and preserves PricingDate. Other Episode-creating operations preserve both date fields from the previous Episode.

Operations that preserve the Episode:

- CommitQuote;
- ReplaceFirmQuote;
- PresentQuote;
- InvalidateQuote;
- ExpireQuote;
- ExtendValidUntil;
- ChangeContactOwner;
- Hit/Close/Cancel terminate rather than create another Episode.

## 20. Why some apparently similar operations remain distinct

### ReplaceFirmQuote versus InvalidateQuote + CommitQuote

Replacing a FirmQuote is one business operation.

Do not model it as an explicit Invalidate followed by Commit. Replacement does not imply that the customer rejected the old price or that the business separately decided to invalidate it.

From Presented, replacement immediately makes the new Quote the current FirmQuote and moves to PendingPresentation. The old Presentation remains historical/latest and the old Quote is no longer normally Hit-eligible.

### InvalidateQuote versus ExpireQuote

Both can lead to Pricing, but:

- InvalidateQuote is a business decision to withdraw;
- ExpireQuote is validity/time semantics.

They remain distinct Domain operations.

### RequestRepricing versus InvalidateQuote

RequestRepricing creates a new PricingEpisode because customer/contact feedback starts a new logical pricing round before presentation.

InvalidateQuote preserves the current PricingEpisode.

### ContinueAfterAway versus simple repricing

ContinueAfterAway creates a PresentationAwayOutcome and a new PricingEpisode whose Origin retains that immutable Away outcome value.

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

## 22. Date semantics summary

All local dates below are BusinessEntityLocalDate and are interpreted under RfqCase.BusinessEntity.

- OpenDate: Case initiation/open date.
- PricingDate: local date to which a PricingEpisode's pricing belongs.
- AssumedTradeDate: trade date assumed for the PricingEpisode when evaluating trade-date-dependent terms; it is not the eventual executed TradeDate.
- PresentationDate: local date on which the Quote was presented to the customer.
- HitDate: local date on which the customer and desk agreed.
- AwayDate: local date on which a specific Presentation became Away.
- CloseDate: local date on which the Case itself was closed.
- CancellationDate: local date on which the Case was cancelled.

PricingDate, AssumedTradeDate, PresentationDate, and HitDate are distinct business concepts. The current Domain does not require equality among them.

For Hit, normal chronology requires HitDate not to precede PresentationDate. CloseDate remains separate.

Normal Away does not require:

    AwayDate == PresentationDate

Timepoint is used only where absolute time matters, currently HitAt and ValidUntil.

## 23. Operational revision chronology, restore, and durable TraceData

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
    | Applied(CaseCommand)
    | RestoredFrom(TargetVersion)

Published(DraftId) is used only for the initial revision created by PublishDraft:

    transition == Published(...)
    => Version == InitialVersion

Applied(CaseCommand) means an accepted ordinary Domain operation was applied to the immediately preceding revision.

For every accepted ordinary operation after publication:

    next.CaseId == current.CaseId
    next.Version == current.Version.Next()
    next.Transition == Applied(command)

One accepted Domain operation creates exactly one next revision. Rejected operations and pure UI/Application work do not create revisions. An Application composite that executes several accepted Domain operations therefore creates several revisions even if they are persisted in one transaction.

CaseCommand denotes the replayable accepted Domain-operation input needed to reproduce the transition. The exact closed command sum type and its serialization/versioning are deferred to the historical-correction design, but the chronology must preserve enough supplied facts to reproduce the accepted business transition.

### 23.3 Operational Restore

Operational Restore means abandoning the currently effective operational path and returning to a previously valid **Open** Case state so ordinary positive-flow processing can continue.

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

Reinstating an earlier terminal business outcome is a different meaning from operational Restore and is not currently modeled.

Restore additionally produces exactly one superseding TraceData record for the path that was effective immediately before Restore:

- restoring while current is Open produces a superseded Open representation;
- restoring while current is Terminal produces a superseded Effective representation.

Restore does not itself produce a new Effective terminal representation because the target is Open. A later terminal operation after restored processing produces the next Effective TraceData.

### 23.4 Loading, persistence, and concurrency boundary

Ordinary operations normally require only the current RfqCaseRevision.

History-dependent operations may require more:

- Restore requires current revision, target Open revision, and enough retained history to construct the superseding Trace;
- Hit, CloseAway, and Cancel require current revision plus enough retained history to construct the terminal Trace.

This does not imply eager loading or whole-history rewrite. Implementation may query, index, stream, or materialize only the required history.

Application/Persistence owns stale-write protection. For example, a Restore requested while v20 is current should commit only if v20 is still current. A stale request is rejected rather than silently applied against a different current revision.

A Domain operation that returns both a new RfqCaseRevision and TraceData should have those facts persisted atomically.

Restore does not reverse external side effects such as already-sent customer communications, downstream notifications, or booking/integration effects. Such reconciliation belongs to Application/integration workflows.

Operational RfqCaseHistory may have finite retention and may eventually be deleted.

### 23.5 TraceData purpose and top-level structure

TraceData is a durable, self-contained business-activity representation materialized from operational history. It is intentionally smaller than the full operational chronology and is expected to outlive RfqCaseHistory.

    TraceData
    - RecordedBy : ActorId
    - RecordedAt : Timepoint
    - RecordedBusinessDate : BusinessEntityLocalDate
    - Origin : TraceOrigin
    - Representation : TraceRepresentation

    TraceOrigin
    = CaseTermination
    | OperationalRestore
    | HistoricalCorrection

    TraceRepresentation
    - CaseId
    - BusinessEntity
    - CaseOpenDate : BusinessEntityLocalDate
    - InitialContactOwnerId
    - FirstPresentedContactOwnerId?
    - State : TraceState

RecordedBy / RecordedAt / RecordedBusinessDate / Origin describe creation of this TraceData record.

TraceRepresentation is the durable business representation. InitialContactOwnerId is the ContactOwner at publication/open. FirstPresentedContactOwnerId is the ContactOwner at the first customer Presentation and is absent when no Presentation ever occurred. Intermediate ContactOwner changes are not retained in the current Trace model.

RecordedBusinessDate is an explicitly supplied BusinessEntity-local business date. It is not required to be mechanically derived from RecordedAt.

### 23.6 Trace state

    TraceState
    = Effective(EffectiveTrace)
    | Superseded(SupersededTrace)

    EffectiveTrace
    = PresentedClosedTrace
    | UnpresentedClosedTrace
    | CancelledTrace

    SupersededTrace
    = Effective(
          EffectiveTrace,
          Reason : SupersessionReason
      )
    | Open(
          OpenTrace,
          Reason : SupersessionReason
      )

    SupersessionReason
    = OperationalRestore
    | HistoricalCorrection

A superseding TraceData does not reference a previous TraceData by TraceId. It repeats the prior business representation as Superseded so the new record remains self-contained after operational history is deleted.

The earlier TraceData record remains immutable. Its Recorded* metadata is not copied as part of the business representation being superseded.

### 23.7 Durable terms and terminal shapes

Trace terms remove Case-local RfqTermsId while retaining the durable business values:

    TraceTerms
    - ClientId
    - Side
    - SecurityId
    - Notional : NotionalAmount
    - SettlementDateRule

    PresentedClosedTrace
    - Terms : TraceTerms
    - Presentations : NonEmpty<PresentationActivity>
    - Terminal : PresentedClose

    PresentedClose
    - ChangesBeforeTerminal : ActivityChange[]
    - CaseCloseDate : BusinessEntityLocalDate
    - ClosedBy : ActorId
    - ClosedAt : Timepoint
    - Outcome : Hit | Away

    Hit
    - HitDate
    - HitAt

    Away
    - AwayDate
    - Feedback?

    UnpresentedClosedTrace
    - Terms : TraceTerms
    - CaseCloseDate : BusinessEntityLocalDate
    - ClosedBy : ActorId
    - ClosedAt : Timepoint
    - Feedback?

    CancelledTrace
    - Terms : TraceTerms
    - Presentations : PresentationActivity[]
    - Terminal : Cancellation

    Cancellation
    - ChangesBeforeTerminal : ActivityChange[]
    - CaseCancellationDate : BusinessEntityLocalDate
    - CancelledBy : ActorId
    - CancelledAt : Timepoint
    - CancellationReason

    OpenTrace
    - Terms : TraceTerms
    - Presentations : PresentationActivity[]
    - ChangesBeforeSupersession : ActivityChange[]

OpenTrace currently appears only under TraceState.Superseded. Ordinary Open processing is represented by RfqCaseRevision rather than by an Effective Open TraceData record.

A Restore of an Open Case that has never had a Presentation still produces a superseded Open Trace with:

    Presentations = []
    ChangesBeforeSupersession = []

### 23.8 PresentationActivity and ActivityChange

    PresentationActivity
    - PricingContext
    - Quote
    - Presentation
    - ChangesBeforePresentation : ActivityChange[]

    PricingContext
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

TraceQuote and TracePresentation are descriptive names for the durable value shapes; Case-local QuoteId and PresentationId are not retained.

EffectiveValidUntil is materialized from operational chronology. FirmQuote.ValidUntil remains the canonical source-state field. ExtendValidUntil does not move validity onto QuotePresentation and the sequence of extension operations is not currently retained in TraceData.

Current ActivityChange variants are:

    ActivityChange
    = ContinuedAfterAway(
          PreviousQuoteValue,
          AwayDate,
          Feedback?
      )
    | RepricingRequested(
          RejectedQuoteValue,
          Feedback?
      )
    | QuoteOwnerChanged(
          PreviousQuoteOwnerId
      )
    | PricingDateRolled(
          PreviousPricingDate
      )
    | AssumedTradeDateChanged(
          PreviousAssumedTradeDate
      )

ActivityChange is a business-semantic digest, not a copy of CaseCommand or PricingEpisodeOrigin.

Multiple changes between two Presentations are retained in order. The later Presentation carries the resulting current values; ActivityChange retains enough previous value/provenance to explain the path.

### 23.9 Intentional Trace compression

TraceData deliberately does not preserve complete pre-Presentation or operational workflow history.

Before the first Presentation:

- pricing/workflow changes are not retained as ActivityChange;
- the first Presentation has ChangesBeforePresentation=[];
- RfqTerms changes, QuoteOwner changes, PricingDate rolls, AssumedTradeDate changes, and Inquiry-phase RepricingRequested are compressed away;
- ContactOwner is represented only by InitialContactOwnerId and FirstPresentedContactOwnerId?.

After the first Presentation, RepricingRequested is retained because an unpresented rejected FirmQuote value could otherwise disappear from the durable activity record.

The current Trace also does not preserve separate activity variants for ReplaceFirmQuote, InvalidateQuote, ExpireQuote, or ExtendValidUntil. Their distinctions remain important to positive-flow Domain behavior but are not currently required in the durable business-activity digest.

Two distinct customer Presentations remain distinct PresentationActivity values even if their numerical Quote values are equal.

### 23.10 History resolution and lifetime

TraceData exposes no Case-local RfqTermsId, PricingEpisodeId, QuoteId, or PresentationId.

Construction must resolve those identities to durable values while operational history is available. The implementation may use temporary maps, joins, repository indexes, streaming, or another resolved-history representation.

For immutable Case-local entities, repeated use of the same ID across revisions must resolve to the same value.

Some Trace facts are chronological rather than simple ID lookups. EffectiveValidUntil, for example, is the effective value across the relevant interval of one FirmQuote/Presentation occurrence.

Because TraceData may outlive RfqCaseHistory, it must not require the retained revision chronology for interpretation.

Historical correction may later replay Applied(CaseCommand) transitions while operational history remains available. The exact correction language, including any one-way switch into Trace-native EffectiveCommand processing, is intentionally deferred.

### 23.11 Terminal operation results

Hit, CloseAway, and Cancel are still positive-flow business transitions on RfqCase, but at the revision level they additionally materialize durable TraceData.

Conceptually:

    TerminalResult
    - Revision : RfqCaseRevision
    - Trace : TraceData

Operational Restore similarly returns:

    RestoreResult
    - Revision : RfqCaseRevision
    - Trace : TraceData

Persistence appends the Domain-produced facts. The Domain determines Version.Next(), transition provenance, restored Case content, and Trace business meaning; persistence does not reconstruct those semantics after the fact.

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
- TerminalState.

This preserves the two levels of meaning without unnecessary wrapper types.

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

Positive flow is intentionally completed first.

Already-discussed candidate models include:

- operation reversal / Revert-style correction;
- historical-state restore/jump;
- direct amendment/correction of historical facts;
- combinations of reversal plus explicit amendment.

No option is selected yet.

Important concerns already identified:

- correction must preserve auditability;
- historical-state jumps complicate interpretation of effective history;
- "undo the most recent reversible operation" is attractive for simple operational mistakes;
- some true historical corrections need direct fact amendment rather than pretending the original operation never happened;
- external/irreversible side effects must be separated from in-memory state reversal;
- correction must not become a generic mutation escape hatch.

### 26.2 CancellationReason taxonomy

CancellationReason will be typed.

Exact variants are deferred because they interact with exception/correction semantics and statistical treatment.

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
6. implement typed RfqCase states and operations from the transition table;
7. wrap accepted Case operations in RfqCaseRevision chronology with Domain-declared Version.Next() and RfqCaseTransition provenance;
8. preserve explicit Case-local child identity, the separate DraftId identity, and TraceData's deliberate removal of Case-local references;
9. implement RfqDraft as a separate pre-publication model with DraftField/RfqDraftData semantics rather than reviving the old Draft-as-RfqCase-state model;
10. do not interpret RfqCaseHistory as a requirement to eagerly load or rewrite the complete history for ordinary operations;
11. persist revision + TraceData results atomically when one Domain operation produces both;
12. add tests around positive-flow invariants, revision invariants, restore targets, and Trace materialization before broad API/UI rewiring;
13. if a new requirement conflicts with this model, document the business requirement and revisit the model rather than silently adding a bypass.
