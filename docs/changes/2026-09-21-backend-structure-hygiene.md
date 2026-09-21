# 2026-09-21 - Backend Structure and Hygiene (Commit 2)

This commit completes the mechanical structure pass deferred by the semantic Domain
refactor.

## Changes

- Applied one top-level type per file across `Rfq.Domain` and `Rfq.Application`.
- Moved `CaseMemo` to `Rfq.Domain/Rfqs/CaseMemos`.
- Moved revision types to `Rfq.Domain/Rfqs/Revisions`.
- Completed the Domain feature folders for identities, lifecycle state, quotes,
  transitions, values, and errors.
- Organized Application code under abstractions, authorization, RFQ use cases, quote
  use cases, queries, and grid configuration.
- Preserved the existing `Rfq.Domain` and `Rfq.Application` namespaces. Folder names
  describe feature ownership and do not create namespace churn.
- Aligned `.editorconfig` with the stable layer-namespace policy.
- Confirmed that warnings-as-errors is already enabled centrally in
  `Directory.Build.props`; no duplicate setting was added.

## Compatibility

- No business behavior, HTTP contract, persistence model, or database schema changed.
- No frontend files changed.
- No migration is required.

## Documentation comments

The XML-comment coverage review found no established public-documentation build or
CS1591 policy. Blanket comments that only restate type names were intentionally not
introduced. Comments should be added where they explain a non-obvious contract or
invariant, and a documentation-file policy can be introduced separately if public API
documentation becomes an artifact of the build.
