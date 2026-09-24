# RFQ Domain Rework — Handoff

## 1. What this work is trying to accomplish

The repository contains an RFQ application whose earlier Domain model grew around Draft/Revision/Requested/Confirmed workflow concepts and implementation constraints.

The current redesign is intentionally rebuilding the Domain from business meaning first.

The sequence is:

1. establish a coherent positive-flow RfqCase aggregate;
2. commit that model as canonical documentation;
3. design RfqDraft separately;
4. design exception/correction semantics;
5. design Application use cases/authorization/composites against the settled Domain;
6. then reconcile persistence, API, frontend, and implementation.

The positive-flow RfqCase model is now considered stable enough to be the canonical starting point.

Read docs/design.md before this file. docs/design.md is authority; this file is a roadmap and discussion handoff.

## 2. What was completed

The positive-flow RfqCase model was redesigned and fully re-walked for consistency.

Major settled decisions include:

- RfqCase is the Aggregate Root.
- Draft is not an RfqCase state.
- A future RfqDraft should be a separate aggregate.
- Case child IDs are Case-local.
- RfqTerms is an immutable Case-level child Entity.
- Notional and SettlementDateRule may change within Inquiry only by creating a new RfqTerms and new PricingEpisode.
- after first Presentation, Terms changes create a new Case rather than mutate the existing Case.
- PricingEpisode is immutable and contains RfqTermsId, QuoteOwnerId, PricingDate, and exactly one PricingEpisodeOrigin.
- PricingEpisode changes on Terms change, QuoteOwner change, PricingDate roll, RepricingRequested, and ContinueAfterAway.
- PricingDate is part of the price-round identity.
- BusinessEntityLocalDate replaced the earlier NaiveBusinessDate concept.
- BusinessEntityLocalDate is a local date under RfqCase.BusinessEntity and does not itself prove "business day."
- SettlementDateRule is ExplicitDate or PricingDateLag.
- PricingDateLag uses non-empty CityCalendarSymbol list, BusinessDayCount >= 0, and Following.
- Quote is immutable and belongs to PricingEpisode.
- at most one FirmQuote is current.
- WorkingQuote stays outside Domain.
- QuotePresentation is a Case-local Entity and is distinct from Quote.
- Presentation outcomes attach to Presentation, not Quote.
- PresentationOutcome is Hit or Away and has no independent OutcomeId.
- CaseOutcome is Presented(PresentationOutcome) or Unpresented(Feedback).
- TerminalState is Closed(CloseDate, CaseOutcome) or Cancelled(...).
- HitDate/AwayDate and CloseDate are different concepts.
- PricingDate == PresentationDate == HitDate is a normal-flow Hit invariant.
- AwayDate is not required to equal PresentationDate.
- Negotiating non-Presented states carry LatestPresentation and optional LatestPresentationAwayOutcome.
- CloseAway reuses an existing latest PresentationAwayOutcome, otherwise creates one.
- Presentation Away can continue into another pricing round without closing the Case.
- ContactOwner changes do not change PricingEpisode or lifecycle state.
- QuoteOwner changes only in Pricing and create a new PricingEpisode.
- current actor/authorization belongs to Application.
- Domain operations are differentiated by semantic business effect/invariant, not by which role/use case invoked them.
- InvalidateQuote, ExpireQuote, ReplaceFirmQuote, RequestRepricing, and ContinueAfterAway remain distinct because their business meanings differ even where state shapes overlap.

The full transition table and invariants are in docs/design.md.

## 3. Documentation restructuring performed

The previous active design/engineering documents described the pre-redesign model and are no longer safe as active authority.

They were archived under docs/_archive/.

docs/design.md is now the canonical Domain authority.

docs/refactoring/ remains historical implementation/refactoring material and is not Domain authority.

The current code has not yet been migrated to the new Domain model.

## 4. Next work item: RfqDraft

RfqDraft appears sufficiently independent that it should be designed in a fresh discussion/session.

Current direction:

    RFQ bounded context
    - RfqDraft [separate Aggregate Root]
    - RfqCase  [Aggregate Root]

Likely Draft behavior:

- create;
- amend;
- discard;
- publish -> create a valid RfqCase.

Likely creation rule:

- CreateDraftFromCase(source Case), used when customer changes Terms after a Case has entered Negotiating.

Important: do not add Draft back into RfqCase.State.

Questions to settle in the Draft discussion:

- exact Draft fields;
- Draft identity;
- Active/Published/Discarded lifecycle shape;
- which fields may be incomplete;
- amend rules;
- publish invariants;
- mapping from Draft to RfqCase, initial RfqTerms, and initial PricingEpisode;
- whether SourceCaseId / DraftOrigin is a Domain fact or only Application/audit lineage;
- how ownership fields are initialized;
- whether Draft has its own BusinessEntityLocalDate facts.

## 5. Then: exception/correction Domain

Exception/correction was intentionally deferred until positive flow was stable.

Several approaches were discussed but not selected.

### 5.1 Reversal / Revert

Concept:

- record an explicit reversal of a prior Domain operation;
- restore the prior effective state while retaining audit history.

Why attractive:

- clear operational mental model for recent mistakes;
- resembles "git revert" rather than deleting history;
- can preserve reason/actor/audit separately.

Open concerns:

- not every operation is safely reversible;
- external side effects may already exist;
- reverting through multiple later operations can become ambiguous;
- true historical data corrections are not always well represented as reverse-the-last-operation.

### 5.2 Historical-state restore / jump

Concept:

- select a prior historical state and make it current again.

Why attractive:

- easy mental model for "go back to what the Case looked like then."

Open concerns:

- effective history becomes a graph rather than a simple sequence;
- later facts may refer to entities created after the target state;
- deciding which outcomes/events become ineffective is difficult;
- can make analytics/audit interpretation ambiguous.

### 5.3 Direct amendment / correction

Concept:

- correct a historical/current business fact explicitly.

Why attractive:

- matches genuine back-office correction where the historical fact itself was wrong;
- does not pretend a different business operation occurred.

Open concerns:

- can become a generic mutation escape hatch;
- needs strict typed correction targets and authorization;
- must distinguish correction from ordinary positive-flow operations.

### 5.4 Likely eventual combination

A combined model may be appropriate:

- Revert/Reversal for recent reversible workflow mistakes;
- explicit typed Amendment/Correction for genuinely wrong historical facts;
- avoid unrestricted historical-state jump unless a concrete use case requires it.

Do not treat this as decided. Re-open from business use cases.

## 6. Then: Application use cases and authorization

After Draft and exception semantics, map user workflows to Domain operations.

Known principles:

- authorization/current user is Application responsibility;
- actor difference alone does not justify separate Domain operations;
- Domain operations are split when their semantic effect/invariants differ;
- an Application use case may chain multiple Domain operations in one transaction;
- current BusinessEntityLocalDate is resolved outside Domain;
- IDs, audit timestamp, and audit actor are supplied outside Domain.

Likely use cases to revisit:

- Contact Owner handoff;
- takeover requiring request/acceptance;
- management reassignment;
- Quote Owner handoff;
- Present;
- request repricing;
- ContinueAfterAway;
- CloseAway;
- Hit;
- Cancel;
- PricingDate roll + CommitQuote;
- Draft publish;
- Draft from existing Case.

A prior rule of thumb still applies:

"Does splitting this into independently callable Domain operations allow invalid business state?" is more important than "does this touch one entity or several?"

## 7. Important intentionally deferred items

Do not accidentally decide these while implementing unrelated work:

- exact CancellationReason variants;
- Case-level Presented-Away close disposition taxonomy;
- customer outcome versus operational close reason;
- typed Feedback taxonomy for statistics;
- foreign-market explicit settlement-date context;
- settlement amount/currency/FX rules;
- Case copy/lineage semantics;
- persistence loading strategy for historical child entities;
- DTO/API migration shape;
- event/audit schema under the new model.

## 8. Discarded/stale concepts to watch for in the current code

The current implementation/docs history may contain older concepts that should not automatically survive migration:

- Draft as RfqCase state;
- Requested/Unassigned RfqCase state;
- RfqRevision as the main Terms/versioning mechanism;
- QuoteConfirmed as the current conceptual quote state;
- Repricing as a Domain state;
- notes inside Domain;
- actor IDs embedded into Domain facts solely for audit;
- Quote outcome directly on Quote;
- OutcomeId;
- a two-axis ClientState x QuoteWorkState model;
- reconstructing Presentation solely from "Presented state" without a QuotePresentation entity.

When Codex sees these in code, it should treat them as migration targets, not as evidence that docs/design.md is wrong.

## 9. Recommended next-session start

Start a fresh session for RfqDraft.

Give it the repository and instruct it to:

1. read docs/design.md;
2. read this handoff;
3. treat RfqCase positive flow as fixed unless a concrete contradiction is found;
4. design RfqDraft as a separate aggregate;
5. stop and discuss before changing RfqCase;
6. do not start exception/correction design until RfqDraft has been settled/documented.

After RfqDraft, use another focused pass for exception/correction if the discussion becomes large.

