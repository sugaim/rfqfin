# JPY Corporate Bond RFQ — Canonical Domain Design

This document is the active canonical design authority for the RFQ domain.

It intentionally describes the target business model, not necessarily the current implementation. The current code base still contains concepts from the previous model. When code and this document disagree on domain meaning, this document is authoritative until the implementation is migrated.

Historical documents are retained under docs/_archive/ and docs/refactoring/. They are useful context, but they are not active authority.

Git history is the version history of this document.

## 1. Purpose and current scope

This system supports an internal JPY corporate-bond RFQ workflow.

The business interaction is roughly:

- a customer inquiry is received;
- a Contact Owner owns the customer-facing interaction;
- a Quote Owner owns pricing responsibility for a pricing round;
- one or more firm quotes may be created;
- a firm quote may be presented to the customer;
- a presented quote may Hit or go Away;
- an Away result may end the Case or may cause another pricing round;
- a Case may also end without any quote ever being presented;
- invalid/duplicate/created-in-error Cases are cancelled rather than treated as ordinary Away outcomes.

The RFQ lifecycle ends at RFQ outcome. Booking/ticket creation is downstream and is not part of this domain.

The current scope is deliberately bond-specific. Do not generalize the model for multi-asset use without a concrete requirement.

## 2. Design intent

The design has two goals that must be kept together:

1. make invalid business states difficult or impossible to represent;
2. avoid turning every workflow detail, actor distinction, UI operation, or audit fact into a Domain concept.

The current model therefore distinguishes:

- durable business facts from workflow/UI convenience;
- Domain state validity from actor authorization;
- one business operation from another based on semantic effect, not on who invoked it;
- current aggregate state from historical data that may exist in persistence but need not be loaded as an in-memory collection;
- absolute time from Business-Entity-local calendar dates;
- a quote from the act of presenting that quote;
- the outcome of a presentation from the final closure of the Case.

## 3. Domain/Application boundary

### 3.1 Domain owns validity and deterministic business meaning

An operation belongs in Domain when its meaning or invariant is required to prevent an invalid RFQ state.

Examples:

- a Presented state must point to the same Quote as the current FirmQuote;
- a Hit must apply to the current presentation and must occur no later than FirmQuote.ValidUntil;
- PresentationDate must match the PricingDate of the PricingEpisode whose Quote is being presented;
- a PricingEpisode change creates a new immutable PricingEpisode rather than mutating the previous one;
- a PricingEpisode created for changed Terms must refer to the new RfqTerms;
- a Close-as-Away from a Negotiating state must use the latest presentation and must ensure that presentation has an Away outcome.

### 3.2 Application owns actor, authorization, orchestration, and external context

Domain operations are not split merely because different actors, roles, desks, or approval routes invoke them.

Examples:

- a user handoff, a management reassignment, and a takeover workflow may all ultimately invoke the same Domain ownership-change operation if their Domain effect is identical;
- authorization such as "only the Contact Owner may present" belongs in Application;
- current user, current BusinessEntityLocalDate, external calculation, IDs, persistence, transaction management, and audit actor/timestamp belong outside Domain;
- an Application use case may compose multiple Domain operations atomically.

Conversely, two operations may share the same source and target state but remain different Domain operations when their business meaning differs. In particular, ExpireQuote and InvalidateQuote are not collapsed merely because both remove the current FirmQuote; expiry and an explicit business withdrawal are different facts.

### 3.3 Application composites do not redefine Domain semantics

A composite use case may execute several Domain operations in sequence.

If multiple operations create a new PricingEpisode, each operation creates its own immutable episode. Intermediate episodes are valid even when no Quote was created from them. The final episode becomes current. Historical persistence may expose the intermediate episodes.

Do not reinterpret "number of PricingEpisodes" as "number of quotes actually priced."

### 3.4 Explicitly Domain-external for now

The following are intentionally outside the RfqCase aggregate model:

- actor/role authorization;
- CurrentUser;
- audit actor and recorded/executed timestamps;
- WorkingQuote and other mutable pricing work-in-progress;
- ContextNote, TraderNote, and SalesNote;
- UI state;
- Draft creation/editing. A future RfqDraft is expected to be a separate aggregate, not an RfqCase state;
- the complete historical collections of Terms, Episodes, Quotes, Presentations, and Outcomes;
- correction/reversal/amendment mechanics;
- booking;
- final settlement amount/currency/FX-conversion rules.

These decisions are intentional. Do not reintroduce these concepts into RfqCase solely because they exist in the workflow.

## 4. Naming and notation

The domain documentation uses C#-compatible PascalCase for type names, field names, and operation names. This is intentional so that the conceptual model maps cleanly to the implementation.

Use:

- RfqCase, RfqTerms, PricingEpisode;
- CaseId, QuoteId, PresentationId;
- CommitQuote, PresentQuote, ContinueAfterAway.

Do not mix snake_case into the domain vocabulary.

Use Id rather than ID in type/member names.

Variation names should describe the semantic variant, not implementation mechanics.

Examples:

- SettlementDateRule.ExplicitDate;
- SettlementDateRule.PricingDateLag;
- PresentationOutcome.Hit;
- PresentationOutcome.Away;
- CaseOutcome.Presented;
- CaseOutcome.Unpresented.

## 5. Identity model

RfqCase is the Aggregate Root and has CaseId.

Child identity is Case-local unless explicitly stated otherwise.

Current Case-local IDs:

- RfqTermsId;
- PricingEpisodeId;
- QuoteId;
- PresentationId.

The complete persistent identity of a child is therefore conceptually (CaseId, LocalId).

No separate OutcomeId is introduced. A Presentation outcome is identified by its PresentationId because one Presentation has at most one effective outcome.

Typed references may still be used where the type must prove the existence/type of a referenced fact. For example, PresentationAwayOutcomeRef semantically means "the Away outcome for this Presentation exists"; it is not an arbitrary PresentationId wrapper constructible from any value.

## 6. Scalar and supporting Domain Objects

### 6.1 BusinessEntity

BusinessEntity is a Domain Object whose underlying representation may be a string.

It identifies the organizational/business entity that owns the Case and provides the local-date context for all BusinessEntityLocalDate values in the Case.

BusinessEntity is immutable for an RfqCase. No normal Domain operation changes it.

The exact vocabulary of BusinessEntity is intentionally not fixed here.

### 6.2 BusinessEntityLocalDate

BusinessEntityLocalDate is a Domain Object representing a local calendar date in the context of RfqCase.BusinessEntity.

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
- PresentationHitOutcome.HitAt.

### 6.4 CityCalendarSymbol

CityCalendarSymbol is a Domain Object backed by a string and identifies a city/business calendar used by settlement-date lag calculation.

### 6.5 Feedback

Feedback is currently free-form optional string data.

This is deliberately provisional. Future statistics/analytics may require a typed disposition plus optional free text. Do not infer a stable taxonomy from the current string.

## 7. Aggregate shape

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
    - Notional
    - SettlementDateRule

All fields are immutable on a particular RfqTerms instance.

"Changeable" means that a Domain operation may create a new RfqTerms with a new RfqTermsId and make it current. It never means mutating an existing RfqTerms.

Current same-Case change policy:

- ClientId: no normal change operation;
- Side: no normal change operation;
- SecurityId: no normal change operation;
- Notional: may change only while the Case is in Inquiry phase;
- SettlementDateRule: may change only while the Case is in Inquiry phase.

After the first customer presentation, a change to Notional or SettlementDateRule creates a new Case rather than changing the existing Case. The future Draft/Copy use case is expected to support this workflow.

### 8.1 SettlementDateRule

SettlementDateRule answers "how is the settlement date determined?"

    SettlementDateRule
    = ExplicitDate(BusinessEntityLocalDate)
    | PricingDateLag(SettlementLag)

ExplicitDate means the explicitly supplied BusinessEntity-local date is the settlement date. It remains fixed when PricingDate rolls.

PricingDateLag means settlement date is resolved from the PricingEpisode.PricingDate using SettlementLag. Therefore a PricingDate roll can change the resolved settlement date without creating new RfqTerms.

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
    - Origin : PricingEpisodeOrigin

Meaning:

A PricingEpisode is one logical pricing round for one RfqTerms, owned by one QuoteOwner and attributed to one PricingDate.

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
    | RepricingRequested(
          PreviousPricingEpisodeId,
          QuoteId,
          Feedback?
      )
    | ContinuedAfterAway(
          PreviousPricingEpisodeId,
          PresentationAwayOutcomeRef
      )

Origin is exactly one variant, not a bag/list of causes.

If an Application composite performs two episode-changing Domain operations, two episodes are created in sequence. The current episode is the latest one.

Origin stores only information needed to explain why the new pricing round exists. It does not duplicate data already available from the referenced previous/current entities.

## 10. Quote and FirmQuote

Quote is an immutable Case-local child Entity.

    Quote
    - QuoteId
    - PricingEpisodeId
    - Value : QuoteValue

Current QuoteValue:

    QuoteValue
    = CleanPrice(Price : decimal)
    | Yield(
          YieldValue : decimal,
          YieldConventionId
      )

QuotedBy and QuotedAt are not Domain fields in the current model.

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

A QuotePresentation represents a business proposal event: the Quote was actually presented to the customer.

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
          Outcome : CaseOutcome
      )
    | Cancelled(
          CancellationDate : BusinessEntityLocalDate,
          CancellationReason
      )

Semantics:

- Closed means a valid RFQ Case reached a normal business conclusion;
- Cancelled means the Case should not be treated as an ordinary Hit/Away outcome, e.g. invalid, duplicate, or created in error;
- exact CancellationReason variants are intentionally deferred until exception/correction requirements are designed;
- CancellationReason is expected to be typed, not an unrestricted string.

CloseDate is the date the Case itself was closed. It is distinct from HitDate and AwayDate.

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

and:

    Presentation.PresentationDate
    == PricingEpisode.PricingDate

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

    PresentationHitOutcome.HitDate
    == Presentation.PresentationDate
    == PricingEpisode.PricingDate

    PresentationHitOutcome.HitAt
    <= FirmQuote.ValidUntil

Business-date equality is a real business rule. ValidUntil extending into a later local date does not permit a normal-flow next-day Hit.

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

### 16.9 Time does not mutate state

Clock passage alone does not change Domain state.

An expired FirmQuote may remain structurally current until ExpireQuote or another explicit operation occurs.

Hit always checks HitAt against ValidUntil, so delayed expiry processing cannot allow an invalid Hit.

## 17. Transition graph

Every distinct Domain operation is numbered even when two operations share the same source and target.

### Inquiry transitions

    [T01] CreateCase
          -> Inquiry.Pricing

    Inquiry.Pricing
      --[T02 CommitQuote]---------> Inquiry.PendingPresentation
      --[T07 ChangeRfqTerms]-----> Inquiry.Pricing
      --[T09 ChangeQuoteOwner]---> Inquiry.Pricing
      --[T10 RollPricingDate]----> Inquiry.Pricing
      --[T31 CloseAway]----------> Terminal.Closed
      --[T36 Cancel]-------------> Terminal.Cancelled

    Inquiry.PendingPresentation
      --[T03 ReplaceFirmQuote]---> Inquiry.PendingPresentation
      --[T04 InvalidateQuote]----> Inquiry.Pricing
      --[T05 ExpireQuote]--------> Inquiry.Pricing
      --[T06 RequestRepricing]---> Inquiry.Pricing
      --[T08 ChangeRfqTerms]-----> Inquiry.Pricing
      --[T11 RollPricingDate]----> Inquiry.Pricing
      --[T12 PresentQuote]-------> Negotiating.Presented
      --[T13 ExtendValidUntil]---> Inquiry.PendingPresentation
      --[T32 CloseAway]----------> Terminal.Closed
      --[T36 Cancel]-------------> Terminal.Cancelled

### Negotiating transitions

    Negotiating.Pricing
      --[T14 CommitQuote]--------> Negotiating.PendingPresentation
      --[T21 ChangeQuoteOwner]---> Negotiating.Pricing
      --[T22 RollPricingDate]----> Negotiating.Pricing
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
      --[T34 CloseAway]----------> Terminal.Closed
      --[T36 Cancel]-------------> Terminal.Cancelled

    Negotiating.Presented
      --[T24 ReplaceFirmQuote]---> Negotiating.PendingPresentation
      --[T25 InvalidateQuote]----> Negotiating.Pricing
      --[T26 ExpireQuote]--------> Negotiating.Pricing
      --[T27 ContinueAfterAway]--> Negotiating.Pricing
      --[T28 RollPricingDate]----> Negotiating.Pricing
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
| T01 | CreateCase | Create a valid RfqCase directly in Inquiry.Pricing | new, Origin=Initial | QuoteOwner already known; Draft/Requested is not an RfqCase state |
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
| T12 | PresentQuote | First customer presentation | preserve | create QuotePresentation; PresentationDate=PricingDate; phase becomes Negotiating |
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
| T27 | ContinueAfterAway | Customer proposal goes Away but Case continues | new, Origin=ContinuedAfterAway | create PresentationAwayOutcome; carry Presentation + Away outcome as latest |
| T28 | RollPricingDate | Start a later-date pricing round from Presented | new, Origin=PricingDateRolled | current Presentation becomes latest; no Away outcome is fabricated |
| T29 | ExtendValidUntil | Extend still-valid Presented FirmQuote | preserve | same Quote and same Presentation |
| T30 | Hit | Customer accepts current Presented FirmQuote | preserve/terminal | create PresentationHitOutcome; HitDate=PresentationDate=PricingDate; HitAt<=ValidUntil |
| T31 | CloseAway | Close an Inquiry Case before any Presentation | terminal | CaseOutcome.Unpresented(Feedback?) |
| T32 | CloseAway | Close an Inquiry Case with unpresented FirmQuote | terminal | still CaseOutcome.Unpresented; current Quote gets no Presentation outcome |
| T33 | CloseAway | Close Negotiating.Pricing as Away | terminal | use LatestPresentation Away outcome if present; otherwise create it |
| T34 | CloseAway | Close Negotiating.PendingPresentation as Away | terminal | outcome applies to LatestPresentation, not current unpresented FirmQuote |
| T35 | CloseAway | Close current Presented proposal as Away | terminal | create PresentationAwayOutcome for current Presentation |
| T36 | Cancel | Mark Case invalid/non-normal outcome | terminal | no ordinary Hit/Away result is manufactured; typed reason details deferred |
| T37 | ChangeContactOwner | Reassign customer-contact ownership | preserve | state shape and PricingEpisode unchanged |

## 19. PricingEpisode creation matrix

Operations that create a new PricingEpisode:

| Operation | New RfqTerms? | Origin |
| --- | --- | --- |
| CreateCase | yes, initial | Initial |
| ChangeRfqTerms | yes | RfqTermsChanged(previous Episode) |
| ChangeQuoteOwner | no | QuoteOwnerChanged(previous Episode) |
| RollPricingDate | no | PricingDateRolled(previous Episode) |
| RequestRepricing | no | RepricingRequested(previous Episode, QuoteId, Feedback?) |
| ContinueAfterAway | no | ContinuedAfterAway(previous Episode, PresentationAwayOutcomeRef) |

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

ContinueAfterAway creates a PresentationAwayOutcome and a new PricingEpisode whose Origin references that outcome.

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
- PresentationDate: local date on which the Quote was presented to the customer.
- HitDate: local date on which the customer and desk agreed.
- AwayDate: local date on which a specific Presentation became Away.
- CloseDate: local date on which the Case itself was closed.
- CancellationDate: local date on which the Case was cancelled.

Normal Hit requires:

    PricingDate == PresentationDate == HitDate

CloseDate remains separate.

Normal Away does not require:

    AwayDate == PresentationDate

Timepoint is used only where absolute time matters, currently HitAt and ValidUntil.

## 23. Historical persistence versus aggregate loading

The business model relies on historical facts:

- prior RfqTerms;
- prior PricingEpisodes;
- prior Quotes;
- prior QuotePresentations;
- Presentation outcomes.

This does not imply that every historical entity must be loaded as an in-memory collection on RfqCase.

The current state carries only historical facts required for current business validity, e.g.:

- LatestPresentation;
- LatestPresentationAwayOutcome?.

Persistence/read models may retain and query the complete history.

The exact persistence strategy is not part of this positive-flow Domain decision.

## 24. Intentional negative decisions

The following earlier ideas were considered and rejected or deferred. Do not restore them casually.

### Requested / Unassigned RfqCase state

Rejected for the current RfqCase model.

RfqCase enters Domain only after QuoteOwner is known. Pre-publication work belongs to future RfqDraft/Application workflow.

### Draft as RfqCase state

Rejected.

A future RfqDraft is expected to be a separate aggregate with its own amend/discard/publish lifecycle.

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

## 25. Deferred domain topics

The following topics are deliberately not completed in this document.

### 25.1 Correction / reversal / historical amendment

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

### 25.2 CancellationReason taxonomy

CancellationReason will be typed.

Exact variants are deferred because they interact with exception/correction semantics and statistical treatment.

### 25.3 Presented Case close disposition

No separate Case-level Presented-Away close classification is modeled yet.

Future analytics may require typed dispositions such as ClientWithdrew, NoResponse, PriceRejected, TradedElsewhere, or operational late-close reason.

### 25.4 RfqDraft

Draft is expected to be a separate aggregate within the RFQ bounded context.

Candidate lifecycle:

- amend;
- discard;
- publish -> create RfqCase.

Creating a Draft from an existing RfqCase is a likely rule/use case for "same customer/security, changed Terms after presentation."

Detailed design is intentionally deferred until this RfqCase positive-flow model is committed.

### 25.5 Case copy / lineage

Whether a copied/derived Case retains SourceCaseId as Domain lineage or only Application/audit metadata is not decided.

### 25.6 Application use cases and authorization

To be designed after the positive Domain model.

Known examples include:

- Contact Owner handoff/takeover/management reassign;
- Quote Owner handoff;
- composites that first return a Case to Pricing then change QuoteOwner;
- current local-date roll + commit;
- Draft publish/copy workflows.

The Domain operations must remain actor-neutral unless actor identity itself becomes a business invariant.

### 25.7 Foreign-market settlement semantics

Current explicit settlement dates are BusinessEntityLocalDate, and PricingDateLag resolves to BusinessEntityLocalDate.

This is an intentional current-scope simplification suitable for the primarily JPY-bond workflow. If foreign settlement requires a distinct market-local date context, extend the model rather than weakening the meaning of BusinessEntityLocalDate.

### 25.8 Settlement amount

Settlement amount/currency/FX-conversion rule is a future orthogonal enhancement.

Example: foreign-currency security settled operationally in JPY.

Do not force this into SettlementDateRule.

## 26. Guidance for Codex / future implementation work

Before changing the RFQ Domain implementation:

1. read this document first;
2. treat current code types such as old Draft/Revision/Requested/Confirmed state models as migration input, not authority;
3. do not preserve an old abstraction solely because it exists in persistence or API;
4. keep Application authorization separate from Domain state validity;
5. do not introduce generic setters/update methods to make migration easier;
6. implement typed states and operations from the transition table;
7. preserve explicit Case-local child identity;
8. add tests around invariants before broad API/UI rewiring;
9. if a new requirement conflicts with this model, document the business requirement and revisit the model rather than silently adding a bypass.

