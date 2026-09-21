# RFQ Implementation Instructions

These instructions implement the canonical RFQ design.

## Authority

The canonical design under `docs/rfq-design-spec/` is authoritative for current business/domain behavior.

If an implementation instruction conflicts with the canonical design, **follow the canonical design unless it has been explicitly revised**.

## Repository history and current refactor step

Steps `01`–`15` describe the original incremental build sequence and are retained as implementation history/reference.

The repository has already completed that sequence.

For the current post-build backend refactor, use:

```text
16-semantic-domain-refactor.md
```

This step intentionally revises some implementation-shape assumptions that appear in the original steps, including:

- mutable Domain objects
- `Owned` as a Domain boolean
- Open RFQ status stored as a field combination
- generic `EnsureWorkingQuote` as the primary creation behavior
- primitive/string-heavy Application contexts

Where those historical step files conflict with the updated canonical design or step 16, **the updated canonical design and step 16 win**.

A later structure/hygiene step will handle broad file organization, comments, formatter/editorconfig, and warnings-as-errors cleanup. Do not mix that mechanical pass into step 16 beyond files materially rewritten by the semantic refactor.

---

## Expected solution projects

```text
Rfq.sln

src/
  Rfq.Domain/
  Rfq.Application/
  Rfq.Infrastructure/
  Rfq.Api/
  Rfq.DbTool/
  Rfq.Web/

tests/
  Rfq.Domain.Tests/
  Rfq.Application.Tests/
  Rfq.Infrastructure.Tests/
  Rfq.Api.Tests/
```

Do not add a separate UseCases, Shared, Common, or Contracts project for the current design.

---

## Dependency direction

```text
Application    -> Domain
Infrastructure -> Application + Domain
Api            -> Application + Infrastructure
DbTool         -> Infrastructure
```

`Domain` must not depend on EF Core, ASP.NET Core, PostgreSQL, JSON serialization, current-user infrastructure, or frontend concepts.

`Application` is the Use Case layer.

---

## Current implementation sequence

For the current repository state:

1. `16-semantic-domain-refactor.md`
2. review/commit
3. later structure/hygiene instruction (separate commit)

Do not automatically perform step 2 while implementing step 1.
