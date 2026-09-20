# Step 06 — Trader Routing and Ownership

## Goal

Create the Trader active RFQ screen and implement ownership/routing operations.

No pricing yet.

## Read first

- ownership section of `01-domain-model.md`
- ownership transitions in `02-state-transitions.md`
- authorization section in `03-use-cases-and-authorization.md`
- Trader layout in `04-ui-ux.md`

## Implement

### Domain/Application

Represent:

- AssignedTraderId
- Owned

Rule:

```text
Owned = true => owner is AssignedTraderId
```

Implement operations:

- Pick Up
- Release
- Assign To
- Take Over

Follow canonical preconditions and confirmation semantics.

Add the first centralized `IRfqAuthorization` implementation.

Retrofit the existing Revision create/edit/confirm/discard handlers from Steps 03-05 to use this centralized authorization boundary. After this step, Controllers/handlers must not retain ad-hoc authorization checks for those existing operations.

CurrentUser development identity should be configurable so local testing can switch between at least:

- Sales user
- Trader A
- Trader B

Do not scatter authorization conditionals through Controllers.

### Persistence

Persist routing/ownership changes in `CaseCurrent`.

Add concurrency version if not already present.

Record audit events only if event persistence already exists; otherwise keep an explicit application seam and add events in Step 12. Do not build half an event system here.

### API

Trader active list endpoint.

Ownership command endpoints.

### Frontend

Trader screen:

- Active RFQ AG Grid
- Assigned Trader
- Owned indication
- row selection
- Pick Up / Release / Assign To / Take Over actions
- confirmation dialogs where canonical design requires them

No quote editing columns yet.

## Completion criteria

Using two local trader identities:

1. unowned assigned RFQ can be picked up
2. owner can release
3. unowned RFQ can be assigned
4. another trader can take over an owned RFQ after confirmation
5. invalid operations return clear Forbidden/Conflict behavior

## Tests

Domain/Application authorization and transitions.

API permission tests.

At least one concurrency test for two traders attempting ownership change.

## Commit boundary

```text
add trader routing and ownership workflow
```
