# Design Session 04 — ContinuedAfterAway provenance

## Goal

Complete the positive-flow Domain refinement discovered while examining correction pressure around Presentation outcomes and PricingEpisode lineage.

The concrete question was whether `PricingEpisodeOrigin.ContinuedAfterAway` should keep a typed reference to an Away outcome or retain the Away outcome value itself.

## Decision

The canonical Domain now defines:

    PricingEpisodeOrigin
    = ...
    | ContinuedAfterAway(
          PreviousPricingEpisodeId,
          PresentationAwayOutcome
      )

`PresentationAwayOutcomeRef` is removed.

`PreviousPricingEpisodeId` remains because it preserves explicit PricingEpisode occurrence lineage. The `PresentationAwayOutcome` supplies the immutable business fact that caused the continuation.

## Why the Away outcome is retained by value

Immediately after ContinueAfterAway, the current Negotiating state also carries the latest Presentation and its Away outcome. Later operations may present another Quote, at which point the current/latest Presentation information changes while the PricingEpisode created by ContinueAfterAway remains the same Episode.

Therefore the invariant cannot be modeled as a permanent cross-state rule such as:

    Episode.Origin == ContinuedAfterAway
      => current LatestPresentation has an Away outcome

That implication is true at Episode creation time but need not remain true throughout the Episode lifetime.

Retaining the immutable `PresentationAwayOutcome` in the Origin makes the reason for that Episode stable for the Episode lifetime.

The duplication is intentional:

- the state-side Away outcome describes the latest relevant customer Presentation for current business behavior;
- the Origin-side Away outcome explains why this PricingEpisode was created.

Both are immutable facts, so this is not mutable duplicated state requiring synchronization.

## Presentation remains immutable

`QuotePresentation` remains an immutable Case-local child Entity.

Outcome is not embedded as mutable state inside QuotePresentation.

The model remains:

    QuotePresentation
    - PresentationId
    - QuoteId
    - PresentationDate

    PresentationAwayOutcome
    - PresentationId
    - AwayDate
    - Feedback?

Open Negotiating states may carry:

    LatestPresentationAwayOutcome : PresentationAwayOutcome?

An Open Case cannot carry a Hit outcome because Hit is terminal.

## PricingEpisode lineage remains explicit

During the discussion, replacing `PreviousPricingEpisodeId` with the previous changed value was considered.

That was not adopted.

The existing `PreviousPricingEpisodeId` fields allow the immutable PricingEpisode occurrences to form an explicit lineage:

    E4 -> E3 -> E2 -> E1

This remains useful even when some previous values could be derived from other facts.

## Canonical changes

`../../domain.md` was updated before this session record.

The change:

- removes `PresentationAwayOutcomeRef`;
- changes `ContinuedAfterAway` to carry `PresentationAwayOutcome`;
- updates T27 and the PricingEpisode creation matrix;
- documents the intentional value retention in Origin.

No other positive-flow state or identity structure was changed.

## Boundary to the next design unit

This positive-flow refinement is complete.

The next design unit is **operational correction**: correcting an active/same-day RfqCase after an operational mistake by returning to a previously valid Case state and continuing business processing.

That topic is intentionally separated from later **historical correction / corrected persistent business history**.

The earlier abstract correction-history discussion is not required for the operational-correction unit and should not be used as its starting point.
