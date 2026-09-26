# Design Session 09 — Simplified Trace Semantics

## Goal

Reconfirm the Trace simplification that was left pending after Session 08, make the Trace semantic model self-contained, verify that it can be materialized deterministically from canonical RfqCaseHistory, and incorporate the completed model into canonical `docs/domain.md`.

TraceRevision version semantics are intentionally not completed in this design unit.

## Canonical decisions completed

### Trace is a business representation, not an operation log

Trace remains a durable public/analysis-oriented representation of the effective business path. It is intentionally smaller than RfqCaseHistory.

The earlier FirstPresentation-based compression model was removed. Activity selection is now defined directly by operation-to-business-activity mapping.

### Simplified TraceActivity set

The canonical activity vocabulary is:

    TraceActivity
    = TermsAmended
    | Presented
    | Away
    | RepricingRequested
    | Hit
    | Closed
    | Cancelled
    | Reopened

Restore is not an activity.

The operation mapping is canonicalized in `domain.md`. Important distinctions include:

- RequestRepricing -> RepricingRequested;
- RequestRepricingOnAway -> Away;
- CloseCase(Hit) -> Hit then Closed;
- CloseCase(Away.RecordNow) -> Away then Closed;
- CloseCase(Away.AlreadyRecorded) -> Closed;
- ChangeContactOwner / ChangeQuoteOwner / date rolls are not public Trace activities.

### Presented is self-contained

Presented carries the durable Terms, ContactOwnerId, QuoteOwnerId, PricingDate, AssumedTradeDate, Quote, and presentation date/actor/time.

TraceQuote.EffectiveValidUntil is the effective validity for that proposal on the represented business path, not necessarily the value that existed exactly at PresentedAt. Later ExtendValidity operations may therefore change the materialized EffectiveValidUntil without creating a separate Trace activity.

### PresentationId is removed from public Trace

Hit and Away are associated by ordered chronology:

- each requires at least one preceding Presented;
- each applies to the latest preceding Presented;
- one Presented may have at most one Hit/Away outcome.

This makes the public Trace independent of PresentationId while preserving unambiguous association under the current positive-flow model.

### TermsAmended is a full post-change snapshot

TermsAmended carries complete changed-to TraceTerms plus actor/time. It is not a delta and does not carry PreviousTerms.

### Hit/Away are distinct from Closed

Hit and Away represent Presentation outcomes. Closed represents Case closure.

Closed carries CaseClose plus optional typed CloseFeedback. The current CloseFeedback variant is UnpresentedCloseFeedback only, leaving a typed extension point for future close-specific feedback without conflating it with Away feedback.

### RepricingRequested naming and payload

The earlier InternalRepricingRequested working name was shortened to RepricingRequested.

It carries:

- RejectedQuoteValue;
- optional Feedback;
- RequestedBy;
- RequestedAt.

### Trace contexts

The context type is named TraceCaseContext:

    TraceCaseContext
    - Terms
    - ContactOwnerId
    - QuoteOwnerId
    - PricingDate
    - AssumedTradeDate

InitialContext is publication context. EndContext is the endpoint of the represented effective path. Restore continues to use the abandoned pre-Restore endpoint.

### Kind becomes Trigger

TraceRecord now carries TraceTrigger rather than TraceRecordKind.

Current trigger variants are:

    HitClose
    AwayClose
    UnpresentedClose
    Cancelled
    Reopened
    Restore
    HistoricalCorrection

Trigger means why the Trace was materialized. Trigger-to-Digest consistency is one-way; activity shape does not uniquely determine Trigger. This allows HistoricalCorrection to contain, for example, corrected Hit/Closed activities while retaining HistoricalCorrection as its trigger.

Trigger variants currently carry no payload.

## Implementation feasibility verified

The completed Trace semantics can be materialized deterministically from the canonical RfqCaseHistory model without adding new positive-flow Domain inputs.

Notable resolution rules:

- TermsAmended may use the resulting revision's current RfqTerms;
- Presented resolves source-state Terms/owners/PricingEpisode/FirmQuote;
- Presented.EffectiveValidUntil may require forward resolution over later ExtendValidity operations on the same Quote/Presentation;
- RepricingRequested resolves RejectedQuoteValue from the source PendingPresentation FirmQuote;
- Away / Hit / Cancelled / Reopened payloads are directly available from accepted operations or their immutable outcomes;
- Case-local IDs may be used internally as temporary materialization keys even though they do not appear in public Trace.

The current implementation under `src/` has not yet migrated to this target Domain model; this design verifies implementability of the canonical target rather than claiming that the current code already implements it.

## Regulatory/audit review boundary

A multi-jurisdiction review was performed while checking whether ChangeContactOwner needed to remain a public Trace activity.

No requirement was found that makes ContactOwner reassignment itself a necessary public Trace milestone under the current scope. Point-in-time responsibility remains available through InitialContext / EndContext / Presented, while actual business actors remain on the relevant activities.

Several additional business facts were identified as worth later consideration:

- initial customer instruction originator / ReceivedBy / ReceivedAt;
- receipt metadata for customer-originated amendment or withdrawal;
- desk-side actor at Hit if Hit is intended to represent execution rather than only customer outcome;
- richer Trigger/correction provenance.

These were deliberately not added in this design unit. Communications infrastructure, recordings, routing, and similar technical records remain outside this Trace model.

## Canonical documents updated

This design unit updates:

- `../../domain.md`;
- `../topics/correction/case-history-and-trace.md`.

`../handoff.md` is intentionally not updated because the current discussion continues in the same session.

## Intentionally unresolved

The following remain for the TraceRevision design unit:

- TraceRecord -> TraceRevision;
- TraceVersionNumber identity and sequence semantics;
- how latest/previous Trace revision is supplied without inconsistent duplicate inputs;
- final RevisionTraceResult shape;
- HistoricalCorrection append API while RfqCaseHistory remains immutable;
- Trigger/correction provenance payloads;
- the deferred audit/business-fact candidates above.

## Why this design unit is complete

The Trace semantic model no longer depends on pending working decisions from Session 08. Its activity vocabulary, payloads, association invariants, operation mapping, contexts, Trigger semantics, and History materialization rules are now self-contained and canonical.

The remaining uncertainty concerns versioning and correction provenance around Trace, not the business meaning of the Trace itself.
