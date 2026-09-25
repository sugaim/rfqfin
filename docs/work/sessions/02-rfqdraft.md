# Design Session 02 — RfqDraft

## Goal

Design the pre-publication RFQ model as a separate aggregate after the positive-flow RfqCase model had stabilized.

The session also refined several RfqCase concepts that became visible only when the Draft-to-Case publication boundary was examined.

This file is a completion record, not canonical Domain authority. See `../../domain.md` for the current model.

## Completed Domain design

RfqDraft is a separate Aggregate Root with independent DraftId.

Current root shape:

    RfqDraft
    - DraftId
    - BusinessEntity
    - DraftOwnerId
    - Data : RfqDraftData
    - ContactOwnerId : DraftField<ContactOwnerId>
    - QuoteOwnerId : DraftField<QuoteOwnerId>
    - State : RfqDraftState

Incomplete publication fields use:

    DraftField<T>
    = Undetermined
    | Determined(T)

`Undetermined` means not yet determined, not optional/not-applicable.

`RfqDraftData` is intentionally limited to RFQ content being assembled:

    RfqDraftData
    - ClientId
    - Side
    - SecurityId
    - Notional
    - SettlementDateRule
    - AssumedTradeDate

ContactOwnerId and QuoteOwnerId were moved out of RfqDraftData because they are responsibility assignments, not RFQ content. They remain separate root fields rather than being grouped into a speculative common Responsibility object because their semantics, assignment mechanisms, and publication targets differ.

AssumedTradeDate remains inside RfqDraftData. It is not an RfqTerms field, but it is part of the RFQ input being established before publication. Different Copy/Publish behavior alone was not considered enough reason to introduce a separate Draft pricing-context object.

## Lifecycle

    RfqDraftState
    = Active
    | Deleted
    | Published(CaseId)

- Active is editable.
- Deleted is reversible logical deletion and retains the Draft snapshot.
- Published is terminal for that Draft and retains its publication-time content/responsibility snapshot.
- A Published or Deleted Draft may still be used as a CopyDraft source.

## Domain operations

Settled operations:

- CreateDraft
- AmendDraftData
- ChangeDraftOwner
- ChangeDraftContactOwner
- ChangeDraftQuoteOwner
- DeleteDraft
- RestoreDraft
- CopyDraft
- SeedDraftFromCase
- PublishDraft

The operation split was refined during this session. The earlier generic `AmendDraft` was narrowed to `AmendDraftData` after ContactOwner and QuoteOwner were recognized as distinct responsibility assignments with distinct Domain effects.

ChangeDraftContactOwner and ChangeDraftQuoteOwner allow:

- Undetermined -> Determined;
- Determined -> another Determined value;
- Determined -> Undetermined.

Actor authorization is not encoded by these Domain operations.

## Copy, seed, and publish

CopyDraft may use any Draft state.

The new Draft:

- gets a new DraftId and supplied DraftOwnerId;
- preserves BusinessEntity;
- copies ClientId, Side, SecurityId, Notional, and SettlementDateRule exactly as DraftField values;
- resets AssumedTradeDate, ContactOwnerId, and QuoteOwnerId to Undetermined;
- starts Active.

SeedDraftFromCase may use any RfqCase state. It is deliberately a seed mapping, not a complete reverse conversion from Case to Draft.

It seeds current reusable BusinessEntity/RfqTerms facts, while AssumedTradeDate, ContactOwnerId, and QuoteOwnerId are Undetermined.

PublishDraft:

- is allowed only from Active;
- requires all publication DraftFields, ContactOwnerId, and QuoteOwnerId to be Determined;
- receives CaseId, initial child IDs, OpenDate, and PricingDate from external/Application context;
- creates a valid RfqCase in Inquiry.Pricing with initial PricingEpisode Origin=Initial;
- changes the source Draft to Published(CaseId);
- does not carry DraftOwnerId into RfqCase.

Publication is one indivisible Domain meaning; atomic persistence is an Application/Persistence responsibility.

## RfqCase refinements discovered during Draft design

The Draft publication boundary exposed a previously conflated distinction between PricingDate and trade date.

PricingEpisode now has:

    AssumedTradeDate : BusinessEntityLocalDate

SettlementDateRule is:

    ExplicitDate(BusinessEntityLocalDate)
    | TradeDateLag(SettlementLag)

TradeDateLag resolves settlement from PricingEpisode.AssumedTradeDate while the RFQ is open.

PricingDate and AssumedTradeDate are independent Domain facts.

ChangeAssumedTradeDate creates a new PricingEpisode, preserves PricingDate, and does not carry a current FirmQuote into the new Episode. A Quote must be reaffirmed, although its numerical value need not change.

For ExplicitDate settlement, an Open RfqCase requires:

    SettlementDate >= PricingEpisode.AssumedTradeDate

PricingDate, PresentationDate, and HitDate are not required to be equal. Hit chronology only requires HitDate not to precede PresentationDate plus HitAt <= ValidUntil.

Actual executed TradeDate is not currently stored in the RFQ Domain.

## Value-type refinements

The session also established:

- `Notional : NotionalAmount`
- `NotionalAmount.Value >= 0`
- NotionalAmount does not carry currency; the security determines denomination context.
- `CleanPrice` is an explicit Domain value type with no general non-negative invariant.
- `Rate` is an explicit Domain value type for rate-valued facts such as Yield.
- A generic `Price` value type was not introduced.
- `RateSpread` was discussed but intentionally not added to canonical Domain docs because no current Domain fact uses it.

## Settled Application-side policies around Draft

These rules matter to future Application design but are not RfqDraft Domain invariants.

### Editing and responsibility assignment

- DraftOwner may perform ordinary Draft content editing.
- DraftOwner may grant/revoke ordinary edit access to other users.
- GrantedEditor may edit ordinary RfqDraftData only.
- ContactOwner status alone does not grant ordinary Draft editing.
- DraftOwner alone assigns/changes ContactOwner.
- DraftOwner or ContactOwner may manually assign/override QuoteOwner.
- ContactOwner alone may Publish.
- DraftOwner alone performs normal ChangeDraftOwner, DeleteDraft, and RestoreDraft.
- Exceptional management/approval workflows, if required, should be Application workflows invoking the same Domain operations rather than new actor-specific Domain operations.

The important boundary is that differences in authorization do not by themselves imply different Domain operations.

### Edit-grant lifecycle

Edit grants are Application-side access metadata.

- ChangeDraftOwner revokes all existing granted-editor access.
- DeleteDraft preserves grant records but they are ineffective while Deleted.
- RestoreDraft makes the preserved grants effective again.
- Published grants may be retained historically but confer no further edit capability.

### QuoteOwner routing

Security-to-QuoteOwner routing is Application-side defaulting, not a Domain eligibility invariant.

- SecurityId changes should invalidate the old routing result and trigger Application-side clearing/re-resolution of QuoteOwner.
- Manual QuoteOwner override is allowed.
- DraftOwner and ContactOwner may perform the override; GrantedEditor may not.
- A QuoteOwner differing from the normal routing result does not block Publish.
- If the assigned dealer should not own the RFQ, subsequent dealer handoff is expected to resolve it.

The Domain therefore does not require a SecurityId/QuoteOwner eligibility relation.

### Concurrency and stale publication

RfqDraft does not gain a Domain RevisionId merely for concurrency.

Application/Persistence should maintain an optimistic-concurrency version/token.

- Do not use silent last-write-wins for shared Draft updates.
- Publish must be based on the exact Draft version observed by the publisher.
- If the current Draft version differs from the publisher's observed version, publication must be rejected as stale.
- Draft -> Published(CaseId) and creation of the new RfqCase must be persisted atomically.
- Only one Case may be created by publishing a given Draft.
- Exact retry/idempotency API behavior is deferred.

If a future business rule explicitly approves or references a particular historical Draft revision, a first-class DraftRevision identity may become justified. That requirement does not exist today.

## Provenance and lineage

CopyDraft and SeedDraftFromCase do not currently put SourceDraftId/SourceCaseId lineage into the Domain aggregates.

Application/audit persistence should preserve provenance sufficiently that historical source relationships are not irretrievably lost. A first-class Domain lineage model remains deferred until a concrete business rule depends on it.

## Important modeling lessons from this design unit

Several refinements were driven by later requirements rather than preserving the first plausible structure:

- RfqDraftData was first considered fully flat, then responsibility fields were removed when their distinct business meaning became clear.
- A common Responsibility wrapper was considered but not adopted because common categorization alone did not justify a shared Domain object.
- AssumedTradeDate was recognized as semantically distinct from Terms but was not split into another object because no independent lifecycle/invariant/operation set currently requires it.
- Authorization differences were kept outside Domain, while genuinely distinct Domain effects led to separate responsibility-change operations.
- The model avoided adding abstractions such as generic Price, RateSpread, or DraftRevision before concrete requirements justified them.

## Intentionally unresolved

The following were deliberately left for later design:

- exact edit-grant persistence representation;
- exact QuoteOwner routing algorithm and configuration source;
- exact optimistic-concurrency token representation;
- API retry/idempotency semantics;
- audit/revision-history schema;
- whether a future concrete requirement justifies a common Responsibility object;
- first-class lineage semantics;
- correction/reversal/historical amendment of RfqCase;
- the broader Application use-case/authorization document structure.

## Canonical documentation updated

The canonical `../../domain.md` now contains:

- the completed RfqDraft aggregate/lifecycle/operations;
- Copy/Seed/Publish mappings;
- responsibility fields and dedicated operations;
- AssumedTradeDate and settlement refinements;
- NotionalAmount/CleanPrice/Rate value semantics;
- Domain/Application/Persistence boundary notes needed to prevent these concerns from leaking into Domain.

## Why this design unit ends here

RfqDraft positive flow and the immediate Application-boundary questions required to interpret it are now coherent enough to be treated as a completed design unit.

The next topic, RfqCase correction/reversal/historical amendment, is materially different: it changes how historical and effective Case facts are interpreted rather than how a pre-publication Draft is assembled.

The next discussion should therefore start in a fresh session. Necessary shared context is now captured in canonical/working documentation, which reduces dependence on conversational history and lowers the risk of importing Draft-specific design patterns into correction design.
