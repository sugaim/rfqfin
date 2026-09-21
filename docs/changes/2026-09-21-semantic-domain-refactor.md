# 2026-09-21 — Semantic Domain Refactor Design Revision

This design revision supersedes the earlier implementation-shape assumptions where they conflict.

Key changes:

- Domain business state becomes immutable from callers.
- Open RFQ is typed as Active or Presented.
- Active quote state is typed as QuoteRequested(reason) or QuoteConfirmed(quoteId).
- ContactOwnerId / AssignedTraderId are Case-level.
- Open trader ownership becomes typed `Ownership = Unowned | Owned` instead of Domain `bool Owned`.
- Domain transitions are grouped by business transition category.
- `Rfq.Application` remains the Use Case layer; no separate project.
- Application owns repository/auth/ID/time/external I/O/transaction orchestration.
- Quote Confirm coherently produces updated RFQ state plus immutable ConfirmedQuote.
- WorkingQuote and CaseMemo become immutable Domain values updated via transitions.
- WorkingQuote creation is a Domain factory.
- `StateVersion` wraps signed long for Domain/Application concurrency values.
- Category remains master data (`CategoryId` + Name), not enum.
- persistence rehydration is internal to Domain/Infrastructure boundary.
- `QuoteEvent.CaseId` is removed; Case is derived through Quote -> Revision.
- `CreateFromExisting` compares desk/business dates rather than UTC calendar dates.
- reverse-commit event-cursor integration coverage is required.

Implementation is split into:

1. semantic backend refactor (current instruction)
2. later structure/hygiene pass for broad file moves, XML comments, formatter/editorconfig, and warnings-as-errors cleanup

## Implemented in commit 1

- Replaced mutable aggregate methods with immutable transition groups for lifecycle,
  ownership, quote, amendment, WorkingQuote, memo, initial Draft, and Contact Owner changes.
- Replaced flattened open-state combinations with `ActiveRfq` / `PresentedRfq`,
  `QuoteRequested` / `QuoteConfirmed`, and typed `Ownership`.
- Added `RevisionTerms`, typed `QuoteExpiry` / `QuoteConfirmation`, the coherent
  quote-confirmation result, and the `WorkingQuoteFactory` creation boundary.
- Added `StateVersion` and the Domain exception taxonomy, with HTTP conflict and
  validation mappings retained at the API boundary.
- Corrected `InitialRevision` to the actual `CurrentRevision`; transition results now
  carry superseded/discarded revisions explicitly for persistence.
- Corrected `CreateFromExisting` to resolve the source timestamp in the configured
  desk timezone before comparing it with the business date.
- Added migration `RemoveQuoteEventCaseId`; quote-event Case context is now resolved
  through ConfirmedQuote -> Revision -> Case.
- Added Domain/Application/PostgreSQL integration coverage, including cursor-lock
  no-loss behavior and migration from the immediately preceding schema.

No frontend files or HTTP JSON shapes were changed.
