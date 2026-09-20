# Step 03 — First Vertical Slice: Create and List RFQs

## Goal

Create the first end-to-end RFQ flow:

```text
browser -> API -> Application -> Domain -> PostgreSQL -> browser
```

A Sales user can create a minimal Draft RFQ and see it in a grid.

This is intentionally incomplete but fully runnable.

## Read first

- canonical `01-domain-model.md`
- canonical `03-use-cases-and-authorization.md`
- canonical Sales section of `04-ui-ux.md`
- canonical `05-persistence-and-events.md`

## Implement

### Domain

Introduce the minimum Case identity/value model required for:

- CaseId
- ClientId
- SecurityId
- CreatedAt
- CreatedBy
- CategorySnapshot if required structurally
- Draft lifecycle
- minimal initial `RfqRevision` in `Draft` status

Do not introduce all later quote states yet.

### Application

Add use cases/interfaces for:

- CreateDraft
- GetActiveSalesRfqs

Introduce `CurrentUser` abstraction, but use a simple development implementation in API for now.

Do not build full authorization policy yet.

### Persistence

Add the minimum tables/entities:

- `RfqCase`
- `CaseCurrent`
- `RfqRevision` with only the fields needed for an initial Draft at this stage

`Save Draft` must atomically create the Case and its initial Draft Revision, matching the canonical model. `CaseCurrent.CurrentRevisionId` may reference that Draft Revision while the Case lifecycle is Draft.

Create migration.

Keep table design consistent with the canonical final schema so later migrations evolve naturally.

### API

Add Controllers for:

- create draft
- list current RFQs

Keep DTOs explicit and simple.

### Frontend

Sales screen:

- AG Grid Community
- `New` action
- minimal creation form in left work pane or Ant Design drawer/panel
- fields for ClientId and SecurityId may temporarily be plain text identifiers in this step
- created row appears in grid after explicit reload/query
- basic loading/error state

Do not implement autocomplete yet.

## Important behavior

Unsaved `New` UI state is frontend-only until Save Draft is pressed.

Save Draft creates the Case and returns CaseId.

## Do not implement

- Confirm
- full Revision behavior beyond the initial Draft required by Save Draft
- quotes
- trader workflow
- search/autocomplete
- SSE
- background expiry
- bulk actions

## Completion criteria

From a clean DB:

1. open Sales screen
2. create Draft with ClientId + SecurityId
3. API persists the Case + initial Draft Revision atomically
4. reload browser
5. Draft remains visible

## Verify

Run:

```bash
dotnet test
npm test -- --run
npm run build
```

Manual:

- create two drafts
- refresh page
- confirm they are still present
- inspect DB rows

## Tests

Domain/Application:

- Case identity creation
- initial Draft Revision creation
- invalid missing Client/Security rejected

Infrastructure:

- create/read roundtrip against Testcontainers PostgreSQL

API:

- POST create then GET list

Frontend:

- create form calls API
- grid renders returned Draft

## Commit boundary

```text
add first rfq create and list vertical slice
```
