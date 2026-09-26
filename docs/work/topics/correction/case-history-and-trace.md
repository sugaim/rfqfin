# Case History, Operational Restore, and Trace

## Status

This is an active, non-canonical working note.

It records the current design checkpoint for:

- operational RfqCase chronology;
- operational restore;
- durable business-activity TraceData;
- the minimum bridge needed for the later historical-correction design.

It deliberately stops before designing the full historical-correction command language.

When this note disagrees with `../../../domain.md`, the canonical Domain still wins until the agreed changes are incorporated there.

## 1. Starting point, goal, and current progress

### 1.1 Starting point

The canonical positive-flow model treated `RfqCase` as the Aggregate Root and primarily modeled one current valid Case state.

Operational correction introduced a different requirement:

    valid state at v10
      -> ordinary accepted operations
      -> mistaken path through v20
      -> restore the business state that existed at v10
      -> continue ordinary positive flow as a new v21

The first operational-correction note intentionally avoided historical correction and left open:

- whether restore is Domain behavior;
- which versions may be targeted;
- whether terminal Cases may return to Open;
- whether Case-local identities are reused;
- what a Case version means;
- whether an explicit branch model is needed;
- how stale restore and external side effects are handled.

During discussion, TraceData also became necessary. Restore changes which operational path should remain effective, while the business needs a durable, compact representation of customer-facing activity that can outlive the operational Case history.

### 1.2 Goal of this design unit

Define a self-consistent model in which:

1. ordinary positive-flow Domain meaning remains on `RfqCase`;
2. accepted operational changes create a monotone chronology of immutable Case revisions;
3. operational restore re-adopts a prior valid Case state without deleting chronology;
4. terminal operations and restore can materialize durable `TraceData`;
5. TraceData is self-contained and survives deletion of operational Case history;
6. the model leaves a clean bridge to later historical correction without prematurely designing that correction language.

### 1.3 Progress at this checkpoint

The operational chronology, restore semantics, TraceData purpose, TraceData information-compression policy, and their main operation boundaries are now sufficiently self-contained to document.

The remaining large design problem is historical correction itself:

- correcting and replaying recorded ordinary commands while operational history still exists;
- defining an `EffectiveCommand` language for business history that the current RfqCase state machine cannot represent.

Those are intentionally deferred to the next design unit.

## 2. Layered model

The current model has four distinct concepts.

### 2.1 RfqCase

`RfqCase` is one complete, valid business state of an RFQ.

It remains the positive-flow state machine: its state, child entities, and invariants describe what is valid at one business-state boundary.

It is no longer useful to describe `RfqCase` itself as "the latest Case." The latest operational state is the `RfqCase` contained in the current revision.

Conceptually:

    RfqCase
      = one complete valid business state

### 2.2 RfqCaseRevision

`RfqCaseRevision` is one immutable occurrence of an RfqCase state in operational chronology.

Working shape:

    RfqCaseRevision
    - CaseId
    - Version : CaseVersionNumber
    - Case : RfqCase
    - Transition : RfqCaseTransition

Invariant:

    RfqCaseRevision.CaseId == RfqCaseRevision.Case.CaseId

The name `RfqCaseRevision` is preferred over the earlier `CaseVersion` because it describes one chronological occurrence while leaving `Version` available for the monotone number.

### 2.3 RfqCaseHistory

`RfqCaseHistory` means the retained chronology of RfqCaseRevision values for one CaseId.

It is a Domain concept, but this does **not** mean that every operation must eagerly load one in-memory object containing all revisions.

Operational history may also have finite retention. It is expected that sufficiently old RfqCaseHistory may eventually be deleted.

Therefore:

- ordinary operations normally need only the current revision;
- restore needs the current revision and the selected target revision, plus any history required to construct TraceData;
- terminal operations need the current revision plus enough history to construct TraceData;
- implementation may load, stream, index, or query historical facts as needed;
- the Domain model does not require rewriting the entire history on each operation.

### 2.4 TraceData

`TraceData` is a durable, self-contained business-activity record materialized from operational history.

It is **not** owned by RfqCaseHistory and is expected to outlive it.

Conceptually:

    retained operational history
        |
        | materialize
        v
    TraceData
        |
        +-- durable after operational history is deleted

This lifetime difference is an important design constraint:

- TraceData must not require Case-local IDs or CaseVersion chronology for interpretation;
- references needed to construct a Trace are resolved to durable business values;
- operational provenance that has no durable business meaning should not leak into TraceData merely because it exists in RfqCaseHistory.

## 3. Revision transition model

Working transition model:

    RfqCaseTransition
    = Published(DraftId)
    | Applied(CaseCommand)
    | RestoredFrom(TargetVersion)

### 3.1 Published

The initial revision is created when an RfqDraft is published.

    RfqDraft
      --PublishDraft-->
    initial RfqCaseRevision

`Published(DraftId)` records the business provenance of the initial Case chronology.

It is not a replay command. The initial revision already contains the complete initial RfqCase state, so replay does not require the Draft to remain available.

Invariant:

    transition == Published(...)
    => Version == InitialVersion

The current model assumes Draft publication is the only RfqCase creation path. Do not introduce a generalized creation-source hierarchy until a concrete second creation path exists.

### 3.2 Applied

For an accepted ordinary Domain operation:

    current : RfqCaseRevision
    command : CaseCommand

    Apply(current, command)
        -> next : RfqCaseRevision

with:

    next.CaseId == current.CaseId
    next.Version == current.Version.Next()
    next.Transition == Applied(command)

The command represents the complete accepted Domain-operation input needed to reproduce the transition. Exact command schemas are deferred, but replay requirements mean that externally supplied generated identities, actors, and timestamps may need to be part of the command value.

Rejected operations and pure UI/Application work do not create a revision.

One accepted Domain operation creates exactly one next revision.

An Application composite that executes multiple accepted Domain operations therefore creates multiple revisions even if the surrounding persistence transaction is atomic.

### 3.3 RestoredFrom

Operational restore does not apply an inverse command.

Given:

    v10 = selected earlier valid revision
    ...
    v20 = current mistaken path

restore creates:

    v21
    - Version = v20.Version.Next()
    - Case = v10.Case
    - Transition = RestoredFrom(v10.Version)

Important semantics:

- v10 does not physically become current again;
- versions never move backward;
- the mistaken v11..v20 path remains in operational chronology;
- the exact Case-local child identities from v10 are reused;
- subsequent positive-flow operations create new identities normally;
- a terminal Case may be restored to an earlier Open revision;
- a revision on a previously superseded path may itself later be selected as a restore target;
- no explicit Branch/Worldline Domain object is currently required.

Restore targets must:

- belong to the same Case chronology;
- be earlier than the current revision;
- identify an existing valid RfqCaseRevision.

## 4. Version semantics

`CaseVersionNumber` is a small Domain value with monotone successor semantics.

Conceptually:

    CaseVersionNumber
    - Value
    - Next()

For every accepted operation after publication:

    next.Version == current.Version.Next()

This includes restore. Restoring v10 while current is v20 produces v21, not v11.

Because the Domain declares the next Version and every accepted operation creates exactly one revision, a valid chronology is contiguous unless a later concrete requirement introduces a reason for gaps.

The Version is Domain chronology, not merely a database identity or persistence-generated row number.

## 5. Operation boundaries

### 5.1 Ordinary operations

Ordinary positive-flow operations act on the current RfqCaseRevision.

Conceptually:

    CommitQuote(currentRevision, command)
        -> nextRevision

    PresentQuote(currentRevision, command)
        -> nextRevision

    RollPricingDate(currentRevision, command)
        -> nextRevision

The underlying business state transition is still:

    currentRevision.Case
        + command
        -> next RfqCase

The revision envelope adds:

- `Version.Next()`;
- `Applied(command)`;
- the same CaseId.

Persistence appending the already-created next revision is not a leaked Domain mutation; it is storage of a Domain result.

### 5.2 Terminal operations

Terminal operations are special because durable TraceData requires historical facts that are not necessarily present in the current RfqCase.

Conceptually:

    Hit(currentRevision, historyContext, command)
        -> TerminalResult

    CloseAway(currentRevision, historyContext, command)
        -> TerminalResult

    Cancel(currentRevision, historyContext, command)
        -> TerminalResult

where:

    TerminalResult
    - Revision : RfqCaseRevision
    - Trace : TraceData

The Domain determines both new facts. Application/Persistence atomically appends them.

The exact loaded representation of `historyContext` is an implementation concern. The Domain requirement is only that all Case-local facts needed by Trace construction are resolvable.

### 5.3 Restore

Restore is also a special history-dependent Domain operation.

Conceptually:

    Restore(
        currentRevision,
        targetRevision,
        historyContext,
        recordingContext
    ) -> RestoreResult

    RestoreResult
    - Revision : RfqCaseRevision
    - Trace : TraceData

The returned revision re-adopts the target Case state at `current.Version.Next()`.

The returned Trace records that the previously effective/open business representation was superseded by operational restore.

### 5.4 Persistence and concurrency

Application/Persistence owns optimistic concurrency.

A restore requested while v20 was current should be persisted only if v20 is still current at commit time.

Conceptually:

    expectedCurrentVersion == actualCurrentVersion

If another accepted operation has already produced v21, the stale restore is rejected rather than silently applying against a different current state.

The revision append and Trace append produced by one terminal/restore operation should be atomic.

External side effects are not reversed by restore. Customer messages, downstream notifications, booking-side effects, or other integration effects require separate reconciliation workflows.

## 6. Durable TraceData

### 6.1 Purpose

TraceData is not complete operational history.

It is a compact, durable digest of customer-facing business activity and the pricing provenance needed to explain that activity.

It intentionally compresses or discards operational details that have no current durable business requirement.

TraceData must remain interpretable after RfqCaseHistory is deleted.

### 6.2 Trace record metadata and durable representation

TraceData separates metadata about **this record** from the business representation carried by the record.

Current working shape:

    TraceData
    - RecordedBy : ActorId
    - RecordedAt : Timepoint
    - RecordedBusinessDate : BusinessEntityLocalDate
    - Origin : TraceOrigin
    - Representation : TraceRepresentation<TraceBody>

    TraceRepresentation<TBody>
    - CaseId
    - BusinessEntity
    - CaseOpenDate : BusinessEntityLocalDate
    - InitialContactOwnerId
    - FirstPresentedContactOwnerId?
    - Body : TBody

This distinction is intentional:

- `Recorded*` and `Origin` describe when, by whom, and why this TraceData record was produced;
- `TraceRepresentation` is the self-contained business representation that must remain meaningful after operational history is deleted.

`InitialContactOwnerId` is the ContactOwner at Case publication/open.

`FirstPresentedContactOwnerId` is the ContactOwner at the first Presentation and is absent if the Case never reached Presentation.

Intermediate ContactOwner changes are intentionally not preserved in TraceData at this stage. This gives durable visibility into responsibility at Case inception and first customer proposal without turning TraceData into a complete owner-assignment log.

### 6.3 Trace origin

    TraceOrigin
    = CaseTermination
    | OperationalRestore
    | HistoricalCorrection

Meaning:

- `CaseTermination`: a terminal Case operation produced the Trace;
- `OperationalRestore`: restore produced a new Trace record describing a representation that became superseded;
- `HistoricalCorrection`: later historical correction produced the Trace.

`Origin` answers why this TraceData record was created.

This is separate from a SupersededTrace reason, which answers why a previously effective business representation ceased to be effective.

### 6.4 Recorded versus business-event timestamps

`RecordedBy`, `RecordedAt`, and `RecordedBusinessDate` describe creation/recording of this Trace representation.

They are distinct from business-event facts such as:

- PresentationDate;
- HitDate / HitAt;
- AwayDate;
- CaseCloseDate;
- CaseCancellationDate.

Even when the values happen to coincide during ordinary same-day processing, equality is not a general invariant.

## 7. Trace body

Current type hierarchy:

    TraceBody
    = EffectiveTrace
    | SupersededTrace

    EffectiveTrace
    = PresentedClosedTrace
    | UnpresentedClosedTrace
    | CancelledTrace

    SupersededTrace
    = SupersededEffectiveTrace
    | SupersededOpenTrace

### 7.1 PresentedClosedTrace

    PresentedClosedTrace
    - Terms
    - Presentations : NonEmpty<PresentationActivity>
    - Terminal : PresentedClose

    PresentedClose
    - ChangesBeforeTerminal : ActivityChange[]
    - CaseCloseDate : BusinessEntityLocalDate
    - ClosedBy : ActorId
    - ClosedAt : Timepoint
    - Outcome : Hit | Away

    Hit
    - HitDate
    - HitAt

    Away
    - AwayDate
    - Feedback?

The explicit name `CaseCloseDate` distinguishes Case closure from HitDate/AwayDate.

### 7.2 UnpresentedClosedTrace

    UnpresentedClosedTrace
    - Terms
    - CaseCloseDate
    - ClosedBy : ActorId
    - ClosedAt : Timepoint
    - Feedback?

No pre-Presentation pricing/change history is retained here.

### 7.3 CancelledTrace

    CancelledTrace
    - Terms
    - Presentations : PresentationActivity[]
    - Terminal : Cancellation

    Cancellation
    - ChangesBeforeTerminal : ActivityChange[]
    - CaseCancellationDate
    - CancelledBy : ActorId
    - CancelledAt : Timepoint
    - CancellationReason

If cancellation occurs before any Presentation, `Presentations` is empty and no pre-Presentation pricing/change history is retained.

### 7.4 SupersededOpenTrace

    SupersededOpenTrace
    - Terms
    - Presentations : PresentationActivity[]
    - ChangesBeforeSupersession : ActivityChange[]
    - Reason : SupersessionReason

A Trace is still created when restore supersedes an Open Case that has never had a Presentation.

In that case:

    Presentations = []
    ChangesBeforeSupersession = []

because pre-Presentation pricing workflow is intentionally not part of the durable activity digest.

### 7.5 SupersededEffectiveTrace

    SupersededEffectiveTrace
    - Previous : TraceRepresentation<EffectiveTrace>
    - Reason : SupersessionReason

Current reason model:

    SupersessionReason
    = OperationalRestore
    | HistoricalCorrection

`Previous` contains the complete prior business representation, not only its EffectiveTrace body.

This matters because a later historical correction may change Case-level durable facts such as `CaseOpenDate` or `InitialContactOwnerId`. A superseding record must preserve the representation that was previously treated as effective, including those top-level business facts.

Embedding the previous representation duplicates data that may already exist as an earlier TraceData record. This duplication is currently intentional because each durable TraceData should remain self-contained after operational history is deleted and should not require a TraceId/TraceRevision lookup to interpret what was superseded.

The previous record's `Recorded*` metadata and `Origin` are **not** embedded into `Previous`; those describe the creation of that earlier record rather than the business representation that was superseded.

A later historical-correction design may revisit this if a stronger durable Trace-record identity model becomes necessary.

## 8. PresentationActivity

Current working shape:

    PresentationActivity
    - PricingContext
    - Quote
    - Presentation
    - ChangesBeforePresentation : ActivityChange[]

    PricingContext
    - QuoteOwnerId
    - PricingDate
    - AssumedTradeDate

    TraceQuote
    - Value
    - CommittedBy : ActorId
    - CommittedAt : Timepoint
    - EffectiveValidUntil : Timepoint

    TracePresentation
    - PresentationDate
    - PresentedBy : ActorId
    - PresentedAt : Timepoint

The exact internal names `TraceQuote` / `TracePresentation` are descriptive here rather than finalized public type names.

### 8.1 EffectiveValidUntil

The canonical source Domain retains:

    FirmQuote
    - Quote
    - ValidUntil

ValidUntil is **not** moved onto QuotePresentation.

Trace construction materializes the effective ValidUntil that applied to that Presentation occurrence.

Example:

    v10 Presented P3, ValidUntil=10:00
    v11 ExtendValidUntil -> 10:05
    v12 ExtendValidUntil -> 10:10
    v13 ContinueAfterAway

Trace records:

    P3.EffectiveValidUntil = 10:10

The sequence of extension operations is intentionally compressed away for now.

## 9. ActivityChange

`ActivityChange` is a business-semantic digest, not a copy of the operation log or PricingEpisodeOrigin.

Current variants:

    ActivityChange
    = ContinuedAfterAway(
          PreviousQuoteValue,
          AwayDate,
          Feedback?
      )
    | RepricingRequested(
          RejectedQuoteValue,
          Feedback?
      )
    | QuoteOwnerChanged(
          PreviousQuoteOwnerId
      )
    | PricingDateRolled(
          PreviousPricingDate
      )
    | AssumedTradeDateChanged(
          PreviousAssumedTradeDate
      )

Multiple changes between two customer Presentations are preserved in order.

The later Presentation contains the resulting current PricingContext/Quote values; each ActivityChange stores enough previous information to explain the transition.

Example:

    Presented 100.10
      -> ContinueAfterAway("too expensive")
      -> RollPricingDate
      -> Presented 99.80
      -> Hit

becomes approximately:

    Presentation #1
      Quote = 100.10
      ChangesBeforePresentation = []

    Presentation #2
      Quote = 99.80
      ChangesBeforePresentation =
        - ContinuedAfterAway(
              PreviousQuoteValue = 100.10,
              Feedback = "too expensive",
              ...
          )
        - PricingDateRolled(
              PreviousPricingDate = ...
          )

    Terminal = Hit(...)

## 10. Intentional information compression

TraceData deliberately does **not** mirror every positive-flow operation.

### 10.1 Before first Presentation

Pricing and workflow changes before the first Presentation are not retained as ActivityChange.

The first Presentation therefore always has:

    ChangesBeforePresentation = []

This includes pre-Presentation:

- RfqTerms changes;
- QuoteOwner changes;
- PricingDate rolls;
- AssumedTradeDate changes;
- unpresented quote replacement/invalidation/expiry/repricing details, except where another explicitly retained Trace fact requires them.

The special durable ContactOwner facts are handled separately by:

- `InitialContactOwnerId`;
- `FirstPresentedContactOwnerId?`.

### 10.2 Quote-operation distinctions

The current Trace does not retain separate change variants for:

- ReplaceFirmQuote;
- InvalidateQuote;
- ExpireQuote;
- ExtendValidUntil.

Their operational distinctions remain important in RfqCase Domain behavior, but the current durable activity requirement does not need the complete operation log.

If a future concrete analytics/regulatory requirement needs those distinctions, add the smallest corresponding Trace fact then.

### 10.3 RepricingRequested

RepricingRequested is retained because it can concern an unpresented firm Quote. Without a Trace-level change, the rejected quote value could otherwise disappear entirely from the durable activity record.

### 10.4 Repeated numerical values

Two distinct customer Presentation occurrences remain two PresentationActivity values even when their numerical Quote values are identical.

Trace compresses operational workflow, not distinct customer proposals.

## 11. Case-local identity resolution

TraceData does not expose Case-local identities such as:

- RfqTermsId;
- PricingEpisodeId;
- QuoteId;
- PresentationId.

Those identities are used while resolving source history and are expanded into durable values.

External/master identities may remain where they are themselves durable business facts, e.g.:

- CaseId;
- ActorId;
- ClientId;
- SecurityId;
- QuoteOwnerId;
- ContactOwnerId.

### 11.1 Resolution requirement, not implementation prescription

Trace construction requires access to the complete Case-local facts referenced by the relevant operational chronology.

Conceptually, a resolver may expose:

    TermsById
    EpisodesById
    QuotesById
    PresentationsById
    OutcomesByPresentationId

but these maps are **not** required Domain fields.

Implementation may:

- build temporary maps;
- issue joined queries;
- stream revisions;
- use repository indexes;
- materialize a temporary resolved-history representation.

The Domain requirement is only that referenced facts resolve consistently.

For immutable Case-local entities, the same ID appearing across multiple revisions must resolve to the same value.

### 11.2 Chronological resolution

Some Trace facts are not a simple ID lookup.

`EffectiveValidUntil`, for example, depends on the chronology of the same FirmQuote/Presentation occurrence across ExtendValidUntil operations.

Therefore Trace construction conceptually performs both:

    immutable fact resolution:
        Case-local ID -> immutable entity/value

and:

    chronological fact resolution:
        occurrence + relevant version interval -> effective value

## 12. Actor and timestamp refinements required in the canonical Domain

Trace construction exposed durable actor/timestamp facts that are not present in the current canonical RfqCase model.

The intended canonical refinements are:

    Quote
    - ...
    - CommittedBy : ActorId
    - CommittedAt : Timepoint

    QuotePresentation
    - ...
    - PresentedBy : ActorId
    - PresentedAt : Timepoint

    TerminalState.Closed
    - ...
    - ClosedBy : ActorId
    - ClosedAt : Timepoint

    TerminalState.Cancelled
    - ...
    - CancelledBy : ActorId
    - CancelledAt : Timepoint

`ActorId` identifies the actual human/service actor that performed the action.

It is not the same concept as authorization responsibility:

- QuoteOwnerId remains pricing responsibility;
- ContactOwnerId remains customer-contact responsibility;
- ActorId records who actually performed the operation.

Example: a dealer may remain QuoteOwner while a Sales user or streaming service commits an already-authorized price.

No separate Human/System actor sum type is introduced until actor kind itself becomes business-significant.

Actor/time for ExtendValidUntil is intentionally deferred. Trace currently preserves only the effective ValidUntil, not the extension-event sequence.

## 13. Multiple Trace records

A Case may produce multiple durable TraceData records over its life.

Example:

    v20 Hit/Close
      -> Trace #1: Effective

    v21 Restore(v10)
      -> Trace #2: Superseded by OperationalRestore

    ...
    v30 CloseAway
      -> Trace #3: Effective

Later historical correction may add further TraceData records.

This is append-only business history. A later Trace does not rewrite an older TraceData record.

A superseding Trace may therefore embed the complete previously effective `TraceRepresentation` even though that representation also exists in an older TraceData record. The duplication is deliberate self-containment, not an indication that the older record was rewritten.

The exact persistence-level revision/sequence wrapper for Trace records is intentionally not decided here. TraceData itself remains identity-light and self-contained.

## 14. Operational-history retention and historical correction

Operational RfqCaseHistory may eventually be deleted while TraceData is retained.

The later historical-correction model therefore has two different periods.

### 14.1 While operational history is retained

Historical correction may:

1. start from the initial Published revision's RfqCase;
2. replay the recorded `Applied(CaseCommand)` sequence;
3. correct one or more recorded command values;
4. regenerate the effective TraceData.

This is one reason `Applied(CaseCommand)` is part of revision provenance rather than storing only state snapshots.

### 14.2 When the current RfqCase command language is insufficient

A later design will allow a one-way switch from normal Case replay into a Trace-native `EffectiveCommand` language.

Conceptually:

    initial RfqCase
      -> corrected CaseCommand replay prefix
      -> switch to effective-history mode
      -> EffectiveCommand
      -> EffectiveCommand
      -> TraceData

Once effective-history mode is entered, replay should not return to RfqCase commands, because the Trace state may then contain business facts that no RfqCase state can represent.

### 14.3 After operational history retention expires

Command-based replay is no longer guaranteed.

Current intended scope is pragmatic: sufficiently old history may no longer support command-based historical correction.

TraceData remains durable and interpretable, but designing arbitrary correction after source history deletion is not a requirement of this checkpoint.

## 15. Aggregate and loading terminology

The earlier canonical document calls RfqCase an Aggregate Root.

Correction/versioning requirements now make that terminology less clear:

- RfqCase remains the positive-flow valid-state model;
- RfqCaseRevision is the operational change unit;
- some operations need historical context;
- TraceData is durable but has a separate lifetime from operational history.

This note therefore deliberately does **not** declare either RfqCase or RfqCaseHistory as the final Aggregate Root.

That terminology should be settled only when the consistency boundary and repository contract are updated together.

Whatever terminology is chosen later, it must not imply:

- all revisions are eagerly loaded for every operation;
- all history is rewritten on every save;
- TraceData is owned by operational history;
- operational history must live as long as TraceData.

## 16. Canonical documentation changes implied by this checkpoint

The current canonical `docs/domain.md` will require reconciliation before this design is considered complete.

Known changes include:

- reconsider the statement that RfqCase itself is the Aggregate Root;
- introduce RfqCaseRevision / CaseVersionNumber / RfqCaseTransition semantics;
- make PublishDraft create the initial `Published(DraftId)` revision;
- wrap accepted ordinary operations in revision semantics;
- introduce operational Restore as Domain behavior;
- update the current statement that correction/reversal mechanics are Domain-external;
- add ActorId and the committed/presented/closed/cancelled actor/time fields;
- update Timepoint usage accordingly;
- remove the current statement that Quote has no committed actor/time;
- define terminal and restore operations as Trace-producing history-dependent operations;
- document TraceData as a durable Domain representation with a lifecycle independent of operational history.

The existing positive-flow RfqCase state machine and its business invariants should remain intact unless one of these revisions exposes a concrete contradiction.

## 17. Deliberately unresolved for the next design unit

The following are not blockers for this checkpoint and should not be guessed into the canonical model yet:

- the exact `CaseCommand` sum type and each replayable command payload;
- exact handling/versioning of generated IDs and actor/timestamp inputs inside CaseCommand;
- the `EffectiveCommand` language;
- the Trace-side intermediate state consumed by EffectiveCommand;
- historical-correction validation rules and correction-of-correction semantics;
- the exact persistence-level Trace sequence/revision wrapper;
- operational-history retention duration;
- final Aggregate Root terminology;
- whether later requirements justify retaining additional currently compressed activity facts.

## 18. Design checkpoint

At this point, operational restore and ordinary Trace generation are treated as a coherent design unit:

- RfqCase remains one valid positive-flow state;
- RfqCaseRevision gives that state an immutable operational occurrence;
- Version is monotone and Domain-declared;
- Published, Applied, and RestoredFrom explain revision provenance;
- ordinary operations produce the next revision;
- terminal and restore operations additionally produce durable TraceData;
- restore may re-adopt any earlier same-Case revision, including Open state or previously superseded paths;
- prior chronology remains immutable;
- no explicit branch model is needed;
- TraceData is a compact, self-contained business record and may outlive the operational chronology from which it was materialized;
- historical correction may use replay while history remains available, but its command language is a separate next design problem.

Future enhancement may refine the Trace shape or add more retained activity facts, but those enhancements should start from concrete business requirements rather than re-expanding TraceData into a copy of the complete operational log.
