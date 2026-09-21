# Revision notes

## 2026-09-21 — Semantic Domain Refactor design revision

The canonical design is revised for the post-initial-build backend semantic refactor.

Key changes:

- Domain business state becomes immutable from callers.
- `OpenRfq` is typed as `ActiveRfq` or `PresentedRfq`.
- Active quote state is `QuoteRequested(reason)` or `QuoteConfirmed(quoteId)`.
- ContactOwnerId / AssignedTraderId are Case-level; trader ownership becomes typed `Ownership` on Open RFQ.
- Domain transitions are grouped by business transition category.
- Application remains the Use Case layer and uses typed Domain IDs/values internally.
- Quote Confirm coherently creates ConfirmedQuote and updates RFQ quote state.
- WorkingQuote/CaseMemo become immutable Domain values; WorkingQuote creation uses a Domain factory.
- `StateVersion` wraps signed long for Domain/Application optimistic concurrency.
- Category remains master data (`CategoryId` + Name), not enum.
- persistence rehydration is internal rather than a public business API.
- `QuoteEvent.CaseId` is removed.
- `CreateFromExisting` business-date comparison is corrected.
- reverse-commit event-cursor integration coverage is required.

See `changes/2026-09-21-semantic-domain-refactor.md` and `implementation-instructions/16-semantic-domain-refactor.md`.

## Earlier revision — initial implementation alignment

This revision aligned the canonical design and incremental implementation instructions.

Key changes:

- Step 03 persists the initial Draft Revision with the Case.
- Master/default resolution moves to Step 04; Initial Confirm moves to Step 05.
- Initial Confirm creates a real minimal WorkingQuote; no no-op ensurer is allowed.
- Quote Confirm preconditions and Quoted edit-lock semantics are explicit.
- Quote confirmation is a persisted Quote event for cross-session notification.
- Event cursor semantics are commit-order safe; reverse-commit concurrency is tested.
- Calculation write-back revalidates current Revision, ownership/CaseCurrent, and WorkingQuote versions.
- CreateFromExisting, bulk Confirm/Discard, bulk Close, Revision history, and Quote history have implementation steps.
- ClosedQuoteId is part of the canonical current persistence projection.
- Centralized authorization covers Revision handlers.
- Business `today` is resolved from the desk timezone rather than UTC calendar date.
