# Session 10 — RFQ actor, responsibility, receipt, and audit semantics

## Goal

Review Draft, Case, Case operations, and Trace together from the perspective of business-significant actor/responsibility/audit facts, while keeping only information that can be supplied reliably by the current workflow.

## Completed decisions

- Renamed the generic business actor identity from `ActorId` to `InternalActorId`; it may identify a human or service.
- Introduced `ActorStamp { Actor, At }` as shared representation while preserving semantic containing names such as `Committed`, `Presented`, `Requested`, `Recorded`, `Closed`, `Cancelled`, `Reopened`, and `Restored`.
- Renamed `QuoteOwner` to `PricingOwner` because responsibility belongs to the pricing round rather than one Quote.
- Renamed `ContactOwner` to `ClientContactOwner` to make the customer-facing responsibility explicit without implying ownership of the client.
- Moved `ClientId` out of `RfqTerms` to the RfqCase root. RfqTerms now explicitly means client-originated RFQ transaction/request terms.
- Added immutable `RfqRequestReceipt` with `Received : ActorStamp`. It is required at Draft creation, carried unchanged into the published Case, and normally defaults from Draft creator/time in Application.
- CopyDraft and SeedDraftFromCase create a new RFQ and therefore require a new RequestReceipt rather than copying the source receipt.
- Renamed `ChangeRfqTerms` to `AmendRfqTerms`; ordinary amendment represents a change to the recognized client-originated RFQ terms, while internal pricing assumptions remain PricingEpisode context.
- Kept `AssumedTradeDate` as PricingEpisode context after publication.
- RequestRepricing retains a semantic `Requested` stamp. RequestRepricingOnAway instead carries the Away `Recorded` stamp because establishing Away is the primary business fact of that combined operation.
- Hit now carries `Received : ActorStamp`; the acceptance receipt time remains the validity-check time. Away carries `Recorded`, and Case closure carries `Closed`.
- Renamed Case `CancellationReason.Withdrawn` to `Discontinued`. This means a genuine Case was stopped without an ordinary CaseOutcome and does not assert that the client was the withdrawing actor. Quote invalidation retains its separate `QuoteInvalidationReason.Withdrawn` meaning.
- Restore now retains `Restored : ActorStamp` and free-form typed `RestoreReason`. Application may default a practical value such as "Operational error"; no Domain reason taxonomy is imposed yet.
- HistoricalCorrection remains deferred; no correction API/type was introduced in this unit.

## Trace consequences

- CaseActivityDigest now carries immutable Case-origin facts: ClientId and RequestReceipt.
- TraceCaseContext uses ClientContactOwnerId and PricingOwnerId for point-in-time responsibility.
- RepricingRequested materializes the rejected TraceQuote and point-in-time PricingOwnerId in addition to request feedback/stamp.
- Detailed Draft conversation chronology and client-side representative identity are deliberately not modeled in the current Domain. External communication archives remain the source for those details.

## Information-sufficiency boundary

Every newly required Domain fact has a plausible current source:

- Internal actor is known by the application/session or service identity.
- Request receipt normally defaults from Draft creator/time and can be overridden for proxy/late entry.
- Responsibility owners already exist as structured assignments.
- Restore reason is operator-supplied with an Application default.

ClientRepresentativeId remains intentionally deferred because reliable manual capture is not currently assumed. Its absence means "not currently recorded", not that no external representative exists.

## Canonical update

`docs/domain.md` was updated to reflect this design. The update also re-scanned Draft publication/copy/seed mappings, operation semantics, cancellation/reopen applicability, Trace materialization, and terminology for consistency.

No new topic note is required because this design unit is complete. The active historical-correction topic remains separate and HistoricalCorrection itself was not designed here.

## Completion boundary

This unit is complete. Future work may revisit client-side actor attribution or introduce typed restore/cancellation dispositions only when concrete operational/reporting requirements justify them.
