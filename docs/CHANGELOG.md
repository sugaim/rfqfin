# Revision notes

This revision aligns the canonical design and incremental implementation instructions.

Key changes:

- Step 03 now persists the initial Draft Revision with the Case.
- Master/default resolution moves to Step 04; Initial Confirm moves to Step 05.
- Initial Confirm must create a real minimal WorkingQuote; no no-op ensurer is allowed.
- Quote Confirm preconditions and Quoted edit-lock semantics are explicit.
- Quote confirmation is a persisted Quote event for cross-session notification.
- Event cursor semantics must be commit-order safe; reverse-commit concurrency is tested.
- Calculation write-back revalidates current Revision, ownership/CaseCurrent, and WorkingQuote versions.
- CreateFromExisting, bulk Confirm/Discard, bulk Close, Revision history, and Quote history now have implementation steps.
- ClosedQuoteId is part of the canonical current persistence projection.
- Centralized authorization retrofits previously created Revision handlers.
- Business `today` is resolved from the desk timezone rather than UTC calendar date.
