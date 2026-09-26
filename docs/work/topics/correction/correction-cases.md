# RfqCase Correction Case Catalog

## Status and purpose

This is the active non-canonical regression catalog for the historical-correction design unit.

It preserves the concrete cases used to test correction models. It is intentionally **not** a list of accepted correction operations and must not be read as canonical Domain behavior.

Use it together with:

- `../../../domain.md` for the canonical RfqCaseRevision / Restore / Trace foundation;
- `../../handoff.md` for the active historical-correction target;
- `case-history-and-trace.md` for rationale when needed.

`correction-history-model.md` is an older abstract exploration and is not the current semantic starting point.

The catalog exists because correction abstractions should be tested against concrete failures rather than designed from a generic undo mechanism.

## How to use this catalog

For each proposed correction rule or `Supports(H_rep, H_true)` definition:

1. identify the expected business truth for the case;
2. construct one or more candidate corrected Histories;
3. check that the candidates remain valid under the positive-flow business transition model;
4. check that the proposed Support rule accepts the intended candidate and rejects materially wrong alternatives;
5. do not infer a new Domain rule from one case unless the business distinction is actually required.

The "What this tests" notes below are design pressures and observations. They are not themselves settled requirements.

## Notation

The examples use abbreviated names only for readability:

- `E1`, `E2`: PricingEpisodes;
- `Q1`, `Q2`: Quotes;
- `P1`, `P2`: QuotePresentations;
- `Hit(P1)`, `Away(P1)`: outcomes of a presentation;
- `Closed(...)`, `Cancelled(...)`: terminal Case states.

"Recorded" means the currently effective system representation before correction.

"Expected truth" means the business history that investigation says should be represented, assuming that truth is expressible in the current Domain vocabulary.

## A. Outcome and terminal-state corrections

### Case 1 — Erroneous Hit; no replacement outcome

Recorded:

    ... -> Presented(P1) -> Hit(P1) -> Closed(Hit)

Expected truth:

    ... -> Presented(P1)

The Presentation and Quote were real; only the Hit/terminal outcome was wrong.

What this tests:

- correction may remove a terminal fact and reopen the effective business history;
- correction is not necessarily an inverse of the most recent technical write;
- Presentation truth can survive while its outcome is removed.

### Case 2A — Real Hit; Hit attributes recorded incorrectly

Recorded:

    ... -> Presented(P1) -> Hit(P1, wrong HitDate/HitAt)

Expected truth:

    ... -> Presented(P1) -> Hit(P1, corrected HitDate/HitAt)

What this tests:

- an occurrence can remain real while its business values change;
- support cannot rely only on outcome type or final terminal state.

### Case 2B — Correcting Hit attributes makes the Hit invalid

Recorded:

    ... -> Presented(P1) -> Hit(P1)

Investigation finds that one or more recorded timing/validity facts were wrong, and the corrected values no longer satisfy the Hit invariants.

Subcases considered:

- both `ValidUntil` and `HitAt` were recorded incorrectly, and a coherent corrected combination may still yield a valid Hit;
- the actual customer acceptance happened only after the prior firm quote had expired, implying a new/reaffirmed business proposition may be missing rather than merely a wrong timestamp.

What this tests:

- a correction cannot manufacture an invalid Domain state;
- some apparent field corrections reveal missing business operations/facts;
- if the desired truth is not yet coherent, the business side must resolve it before the system can represent it.

### Case 2C — Recorded Hit; actual result was Away

Recorded:

    ... -> Presented(P1) -> Hit(P1) -> Closed(Hit)

Expected truth:

    ... -> Presented(P1) -> Away(P1) -> ...

What this tests:

- same Presentation, different customer outcome;
- terminal shape alone is insufficient.

### Case 4A — Recorded Hit; actual Away followed by continued pricing, still open

Recorded:

    ... -> Presented(P1) -> Hit(P1) -> Closed(Hit)

Expected truth:

    ... -> Presented(P1)
        -> RequestRepricingOnAway
           => Away(P1) + E2
        -> Q2/P2 or later open state

What this tests:

- correction can replace a terminal suffix with a materially longer open history;
- outcome correction can require recreating downstream pricing history.

### Case 4B — Recorded Hit; actual Away, continued pricing, later Hit

Recorded:

    ... -> Presented(P1) -> Hit(P1) -> Closed(Hit)

Expected truth:

    ... -> Presented(P1)
        -> RequestRepricingOnAway
           => Away(P1) + E2
        -> Q2
        -> Presented(P2)
        -> Hit(P2)
        -> Closed(Hit)

What this tests:

- identical final outcome category does not imply equivalent history;
- which customer proposition was Hit matters;
- support must retain enough historical business meaning to distinguish these paths.

## B. Quote, presentation, and customer-interaction corrections

### Case 3A — Erroneous Away/Continue path; later Quote belongs to original Episode

Recorded:

    E1 -> Q1 -> P1 -> Away(P1)
       -> RequestRepricingOnAway -> E2
       -> Q2 -> P2

Expected truth:

    E1 -> Q1 -> P1
       -> Q2 -> P2

The recorded Away and E2 did not happen; the later Quote/Presentation were real but belong to the continuing original pricing context.

What this tests:

- correction may remove an intermediate occurrence while preserving later business facts;
- later facts may need re-association rather than deletion;
- strict immutable internal references can make direct graph surgery expensive.

### Case 5 — Quote value wrong; Presentation and Hit real

Recorded:

    E1 -> Q1(value = wrong)
       -> P1(Q1)
       -> Hit(P1)

Expected truth:

    E1 -> Q1-or-equivalent(value = correct)
       -> P1
       -> Hit(P1)

What this tests:

- the business interaction may remain the same while quoted content changes;
- support must eventually decide whether internal Quote identity matters or only the customer-facing proposition/value does.

### Case 7 — Presentation timing/date wrong

The canonical model currently carries `PresentationDate`. During the Support discussion, a finer customer-facing `PresentedAt` observation was also considered, but it is not a canonical Domain field.

Recorded and expected truth differ in the relevant Presentation timing information while the Presentation occurrence itself remains real.

What this tests:

- the Presentation occurrence can remain real while its timing is corrected;
- chronology invariants may need revalidation after correction;
- whether correction equivalence needs date-only or finer presentation time remains open.

### Case 12A — Entire Presentation was erroneous

Recorded:

    Inquiry -> Presented(P1) -> Negotiating ...

Expected truth:

    Inquiry ...

What this tests:

- an erroneous Presentation can incorrectly change the Case's historical phase from Inquiry to Negotiating;
- support must be able to express absence of an event, not only corrected values.

### Case 12B — Historical erroneous Presentation removed; current shape otherwise unchanged

Recorded history contains an extra historical P1, but later activity has already produced the same current effective state that the corrected history should have.

Expected truth removes P1 while preserving the later valid business history.

What this tests:

- final/current state equality is too weak;
- correction may be historical-only from the perspective of the current state.

### Case 22 — Wrong current FirmQuote identity/content while remaining Open

Recorded and expected Histories both end Open, but the current firm business proposition is not the one that should be effective.

What this tests:

- Open-to-Open correction is still materially significant;
- current state variant equality is weaker than business-state equality.

## C. Terms and pricing-context corrections

### Case 6A — Terms stored incorrectly; actual pricing used the correct Terms

Recorded:

    Terms = wrong
    pricing/presentation values otherwise reflect the actual correct Terms

Expected truth:

    Terms = correct
    existing downstream business interactions remain valid under those Terms

What this tests:

- an upstream correction can preserve later facts if those facts actually arose from the corrected value;
- correction should not recreate business operations unnecessarily.

### Case 6B — Terms were wrong and pricing really used the wrong Terms

Recorded:

    Terms = wrong
    pricing actually used those wrong Terms

Investigation says the Terms should have been different.

Expected truth is not automatically determined: perhaps pricing should have been redone, perhaps the customer interaction would have differed, or perhaps another business resolution is required.

What this tests:

- correction cannot infer counterfactual business truth;
- "the input should have been X" does not imply "all downstream facts are still valid under X."

### Case 6C — A real Terms change/repricing happened but was not recorded

The final numeric Quote may coincidentally equal the prior one, but the business actually changed Terms and repriced/reaffirmed.

What this tests:

- equal numeric outputs do not prove equivalent business history;
- an omitted business occurrence may need to be restored even if current numbers are unchanged.

### Case 14 — InvalidateQuote versus RequestRepricing confusion

Recorded operation meaning and actual business meaning differ, although the resulting visible state shape may be similar.

What this tests:

- same source/target state shape can still represent different business transitions;
- if that distinction matters historically, the resulting State must preserve enough meaning or the history model must represent it elsewhere.

### Case 15 — RollPricingDate versus ChangeAssumedTradeDate confusion

One operation changes PricingDate; the other changes AssumedTradeDate. Both create a new PricingEpisode under the current Domain.

What this tests:

- structurally similar Episode creation is not necessarily business-equivalent;
- PricingEpisode.Origin is an example of transition meaning deliberately preserved in State.

### Case 16 — Two real Episode-changing operations recorded as one

Expected truth includes two distinct operations/episodes, for example an ownership change followed by a pricing-date roll.

Recorded history contains only one resulting Episode.

What this tests:

- missing intermediate business states can matter;
- a corrected History may need to insert states rather than merely amend a final state.

### Case 17 — Two recorded Episode changes; actual business had one

Recorded history contains an extra Episode that should not exist.

Later Quotes/Presentations may be real and may need association with the surviving Episode.

What this tests:

- removing an intermediate state may require reference reassociation;
- history correction is not always suffix truncation.

### Case 23 — Wrong current Terms while remaining Open

Recorded and corrected cases are both Open but differ in which Terms are current and therefore which pricing context is effective.

What this tests:

- final lifecycle state alone does not characterize correction equivalence.

## D. Ownership and case-level attribute corrections

### Case 8A — QuoteOwner representation-only error

The system recorded the wrong owner, but no actual business handoff occurred.

What this tests:

- an ownership value may simply require corrected historical representation.

### Case 8B — Actual QuoteOwner handoff occurred but was not recorded

The business responsibility really changed and should be represented as the corresponding episode-changing operation/history.

What this tests:

- same final owner value can hide different business history;
- representation-only correction and missing operation are different cases.

### Case 13 — Historical ContactOwner error

The ContactOwner value was historically wrong.

Under the current model, actor authorization belongs to Application rather than defining Domain operation identity.

What this tests:

- not every historical correction pressure creates new Domain semantics;
- Application authorization/audit consequences must be kept separate from business-state validity unless concrete Domain behavior depends on the owner.

### Case 18 — OpenDate wrong

The Case is otherwise valid but its opening local date was recorded incorrectly.

What this tests:

- aggregate-level immutable fields may need historical correction;
- correction cannot be designed only around child entities/transitions.

### Case 19 — BusinessEntity wrong

Recorded BusinessEntity is incorrect.

Because BusinessEntity supplies local-date context, changing it may reinterpret multiple Case dates rather than being an isolated scalar edit.

What this tests:

- apparently small root corrections can have aggregate-wide semantic consequences;
- corrected validity must be checked under the corrected context.

### Case 21 — Upstream/master data was wrong

The RFQ's identifiers may be internally consistent, but an upstream source supplied incorrect reference/master data.

Two possibilities must be distinguished:

- system representation is wrong but business actually used the correct upstream fact;
- business actually acted using the wrong upstream premise.

What this tests:

- evidence and causal truth may live outside the aggregate;
- the second case may require business resolution rather than deterministic correction.

## E. Cancellation, close, validity, and operational timing

### Case 9A — Erroneous Cancel

Recorded:

    ... -> Cancelled

Expected truth may be a still-open Case or an ordinary Closed outcome.

What this tests:

- Cancelled is not a universal correction escape hatch;
- cancellation semantics and correction semantics remain distinct.

### Case 9B — Recorded Cancelled; actual normal Away close

Recorded:

    Cancelled(...)

Expected truth:

    Closed(Presented(Away(...))) or another ordinary close consistent with the actual interaction

What this tests:

- abnormal invalid/duplicate disposition differs from ordinary business outcome.

### Case 9C — Recorded ordinary close; Case was actually invalid/duplicate

Recorded:

    Closed(...)

Expected truth:

    Cancelled(reason = duplicate/invalid/created-in-error or future typed reason)

What this tests:

- the inverse classification error also occurs;
- exact CancellationReason taxonomy remains deferred until correction requirements justify it.

### Case 10A — Close timing wrong

The business outcome is correct but CloseDate is not.

What this tests:

- operational Case closure and customer outcome timing are distinct facts.

### Case 10B — Missing EOD/late close

The customer interaction/outcome is already correct; the Case merely remained operationally open and is closed later.

What this tests:

- some "historical cleanup" is ordinary positive flow, not correction;
- correction scope should not absorb normal late operations merely because they refer to older business activity.

### Case 11A — ValidUntil recorded incorrectly

Correcting ValidUntil may affect whether a recorded Hit was valid.

What this tests:

- correcting one fact can invalidate dependent facts;
- corrected History must satisfy the business invariants as a whole.

### Case 11B — Premature expired invalidation

If InvalidateQuote(Reason=Expired) was invoked before validity elapsed, the normal Domain should reject it.

What this tests:

- operations prohibited by normal invariants should preferably remain impossible rather than become routine correction cases.

### Case 11C — Earlier ValidUntil was wrong, making a previously recorded expired invalidation appear valid/invalid

InvalidateQuote(Reason=Expired) may have been legal relative to the recorded value but wrong relative to corrected business truth.

What this tests:

- correction may change the interpretation of an operation that was locally valid under the old representation.

## F. Correction of correction and partial replacement

### Case 20 — Correction-of-correction A -> B -> A

Original representation A was changed to B, then later investigation establishes that A was in fact the correct business history.

What this tests:

- procedural reversal chains become awkward quickly;
- immutable history revisions can represent the newest effective belief without mutating or erasing prior audit versions;
- Support should compare the effective candidate against intended truth, not against the immediately preceding revision.

### Case 24 — Only part of an earlier correction was wrong

A previous correction fixed one region correctly but changed another region incorrectly.

Expected truth keeps the good corrected prefix/portion and replaces only the wrong portion in the new effective History.

What this tests:

- correction-of-correction is not necessarily whole rollback;
- a revision should represent the best whole History now believed, irrespective of which prior revision introduced each part.

## G. Case identity and multi-Case corrections

These cases are intentionally harder and may require Application/Persistence coordination beyond single-aggregate correction.

### Case 25 — One recorded Case should actually be two Cases

Facts currently represented under one Case correspond to two distinct customer RFQs.

What this tests:

- Case identity correctness precedes child-fact correctness;
- Case-local child IDs make "moving" facts across Cases an identity change at persistence level;
- correction may require split semantics rather than single-History replacement.

### Case 26 — Invalid/duplicate Case versus valid Case with many errors

Two superficially messy records may differ fundamentally:

- one Case should not exist as an ordinary RFQ at all;
- another is a real RFQ whose history contains many mistakes.

What this tests:

- number of erroneous fields/operations is not a valid heuristic for Case identity;
- cancellation/invalidity and correction must not be conflated.

### Case 27 — Two real Cases; facts attached to the wrong Case

Both Case identities are real, but one or more presentations/quotes/outcomes were associated with the other Case.

What this tests:

- correction can require reassociation across aggregate boundaries;
- single-Case atomicity may be insufficient.

### Case 28 — Duplicate/split Cases representing one RFQ

Two Case records jointly contain duplicated, distributed, or mixed history for what business investigation identifies as one RFQ.

Questions include which Case identity survives and how facts are deduplicated/merged.

What this tests:

- surviving aggregate identity can itself be a business decision;
- multi-Case correction may require atomic coordination;
- internal child identity cannot be assumed to survive cross-Case movement unchanged.

## H. Cross-cutting pressures exposed by the catalog

These are not yet formal Support rules. They are recurring pressures that any proposed correction model should survive.

### Current state is insufficient

Cases 4B, 12B, 16, 17, and others show that equal final states can hide different business histories.

### Generic undo is insufficient

Cases 3A, 20, 24, 25, 27, and 28 require preservation, reassociation, splitting, or correction-of-correction rather than simply reversing the latest action.

### Corrected validity is global

Cases 2B, 11A, 11C, and 19 show that changing one fact can alter the validity or meaning of dependent facts.

### Truth cannot be inferred from legality

Cases 6B and 21 show that multiple legally valid corrected histories may exist, while only business evidence can identify the intended one.

### Internal identity and business equivalence may differ

Cases 3A, 5, 12B, 25, and 27 expose a distinction between:

- IDs/references needed for aggregate/persistence integrity; and
- the business facts that a corrected History must preserve.

Exactly where identity is business-significant remains open.

### Command sequences remain useful without being historical truth

Cases 14–17 suggest that ordinary Domain operations and their invariants may be reusable to construct corrected Histories.

A command sequence generated during correction should be treated as a construction/replay witness unless there is independent reason to assert that it is the actual historical sequence.

## Immediate use in the next discussion

The next correction discussion should take candidate definitions of:

    Supports(H_rep, H_true)

and apply them to this catalog.

The first goal is not to define correction APIs. It is to determine which business observations or relationships must be preserved so that one valid History is a sufficient corrected representation of another.
