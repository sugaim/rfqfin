# Operational Correction

## Status

This is a non-canonical working note for the next RfqCase design unit.

It concerns **operational correction of an active Case**, not historical reconstruction of the business history that should permanently be represented.

## Read for this design unit

Read only:

1. `../../../domain.md` — canonical RfqCase Domain;
2. `../../README.md` — working-document rules;
3. `../../handoff.md` — current design target;
4. this file.

Do **not** start by reading:

- `correction-history-model.md`;
- `correction-cases.md`;
- `../../sessions/03-rfqcase-correction-foundation.md`.

Those files contain an earlier, broader and more abstract correction exploration. They are retained for the later historical-correction design unit, but they are not prerequisite context for this operational problem and may anchor the discussion on abstractions that are unnecessary here.

## Problem being isolated

A user may make an operational mistake while a Case is still being worked.

Typical shape:

    valid state at version v10
      -> ordinary commands
      -> current version v20
      -> discover that the operational path after v10 was mistaken

The practical requirement is often:

> return the Case to a state it actually had before, then continue ordinary positive-flow processing from there.

This is materially narrower than historical correction.

Operational correction does not initially need to answer:

- what the permanently corrected customer/business history should be;
- how historical Presented/Hit/Away facts should be minimally represented;
- correction-of-correction over historical truth;
- cross-Case split/merge/reassociation;
- exact audit/regulatory history representation.

Those belong to the later historical-correction unit.

## Working direction

A previous valid Case state is already known to satisfy the positive-flow Domain invariants.

Therefore restoring a prior state is a plausible operational primitive and can be substantially simpler than defining an inverse Domain command for every positive-flow operation.

Candidate version semantics:

    v10 = earlier valid Case state
    ...
    v20 = current Case state

    operational correction selects v10

    v21 = a new current version whose Case contents are restored from v10

Business processing then continues from v21.

Important properties of this direction:

- version numbers do not move backward;
- old versions are not overwritten;
- v10 does not become current again physically;
- the correction creates a new chronological version;
- the restored Case contents may be equal to the earlier version while the operational chronology remains visible;
- subsequent ordinary Domain commands operate from the restored valid state.

This is a working direction, not yet a canonical Domain decision.

## Why this is not inverse-command Undo

An inverse command would require every operation to define how to remove or reconstruct all facts it created.

For operations such as PresentQuote or ContinueAfterAway this can become substantially more complicated than restoring a complete prior valid aggregate state.

The operational requirement also does not require pretending that the mistaken commands never occurred operationally. Their prior versions may remain visible to audit/persistence.

Therefore the initial design should evaluate state restore/jump directly rather than begin by defining inverse commands.

## Important separation

Restoring state does not automatically reverse external side effects.

Examples may include:

- customer communication already sent;
- downstream notifications;
- external system updates;
- booking or other irreversible effects outside the current RFQ Domain.

Reconciliation of external effects is Application/integration work and must not be hidden inside the state-restore semantics.

## Questions intentionally left for the next discussion

### 1. Domain versus Application/Persistence boundary

Should operational correction be a Domain operation such as `RestoreToVersion`, or should Application/Persistence:

1. load an old valid Case representation;
2. reconstruct the aggregate;
3. make that representation the contents of a new current CaseVersion?

The answer should follow from business semantics, not from implementation convenience.

### 2. Allowed restore target

Is correction allowed to select:

- only the immediately preceding version;
- any earlier version of the same Case;
- only versions within the current open lifecycle;
- versions before a terminal state, thereby reopening a terminal Case?

Do not assume unrestricted arbitrary jump until the business semantics are clear.

### 3. Identity on restore

A restore to v10 would naturally restore the Case-local identities present in v10:

- RfqTermsId;
- PricingEpisodeId;
- QuoteId;
- PresentationId.

Confirm whether reusing those exact business occurrences is the intended meaning, rather than allocating new child IDs for equivalent values.

### 4. Version semantics

Clarify:

- whether every accepted ordinary command creates a CaseVersion;
- whether one restore action creates exactly one new version regardless of how far back it jumps;
- whether a restored version records `RestoredFromVersion` or equivalent metadata;
- whether that metadata is business meaning or audit/persistence metadata.

### 5. Branch interpretation

After:

    v10 -> ... -> v20 -> restore(v10) -> v21

the sequence v11..v20 remains part of operational chronology but is no longer on the effective processing path.

Decide whether any explicit branch/finalization concept is needed, or whether the version chronology plus current-version pointer is sufficient.

Do not introduce branch abstractions unless a concrete requirement needs them.

### 6. Concurrency and stale correction requests

Selecting an old version while another command changes the current Case creates an optimistic-concurrency problem.

This is expected to be Application/Persistence responsibility unless a business invariant requires more.

### 7. Interaction with historical correction

Operational restore must not be assumed to solve the later historical-correction problem.

A later design unit will address the persistent/effective business history using a deliberately smaller representation if possible.

## Desired output of the next design unit

The next session should aim to decide:

1. the exact business semantics and naming of operational restore/undo;
2. where the operation belongs across Domain/Application/Persistence;
3. the allowed restore target;
4. child-identity behavior;
5. the minimum CaseVersion semantics needed to support it.

Do not design the historical-correction representation in the same unit unless operational semantics expose a direct dependency.
