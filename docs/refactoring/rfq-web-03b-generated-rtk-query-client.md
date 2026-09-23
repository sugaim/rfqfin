# Codex Instruction — 03b: Generated RTK Query Client Migration

## Position of this task

Repository: `sugaim/rfqfin`

Required baseline:

- 03a implementation: `680d96d4e52cf2ecd7b2cba9b32a14473067310a`
- canonical documentation update for 03b: `7b0473f7b15223d83bb237caf2817168f3bfe856`

Start from current `main` at or after `7b0473f7b15223d83bb237caf2817168f3bfe856`.

Before changing code, read both active canonical documents:

- `docs/design.md`
- `docs/engineering.md`

Their responsibility split remains intentional:

- `design.md` owns business meaning, invariants, workflow, authorization, UX semantics, persistence/business facts, and durable architecture.
- `engineering.md` owns source organization, HTTP/OpenAPI policy, generated-client policy, frontend transport boundaries, runtime/read-model mechanics, testing/tooling, and deployment assumptions.

Files under `docs/refactoring/` are historical implementation instructions. Do not treat them as active authority and do not delete them.

This application has **not been released**. There is no requirement to preserve backward compatibility for private HTTP routes, handwritten frontend transport types, generated artifacts, or obsolete internal abstractions. Do not add compatibility aliases, dual paths, fallback DTOs, or speculative defensive layers for hypothetical older clients.

Likewise, do not make the implementation “defensive” merely because a previous internal shape existed. Implement the target design directly. Keep validation and guards that protect current business invariants or genuinely untrusted/external input; do not preserve obsolete behavior as defensive scaffolding.

---

# 0. 03a review result

03a is accepted.

No blocking Application/API redesign is required before generated-client migration.

The following 03a follow-ups are minor and are intentionally included in 03b:

1. `App.tsx` still owns handwritten `EventSource` transport.
   - move EventSource lifecycle and development identity query propagation out of `App.tsx`
   - keep App responsible only for interpreting wake-up categories and updating app-level change versions

2. some Screen code still interprets RTK Query error shapes directly.
   - current examples include Trader Screen reading `{ data?: ApiProblemDetails }`
   - Sales amendment autosave currently inspects a transport-style `status`
   - RTK Query-specific error representation must stop at the Workspace/transport boundary

3. `services/api.ts` still contains the handwritten endpoint/DTO surface and handwritten cache tags.
   - this is expected after 03a
   - 03b removes it rather than layering generated endpoints beside it

4. `openapi-typescript` and `src/generated/api-schema.ts` are still the intermediate schema-only generation path.
   - remove them after the generated RTK Query client has fully replaced their role

Do not redesign 03a business contracts unless generated-client work exposes a concrete OpenAPI defect. If generated output is awkward:

1. first determine whether the ASP.NET/OpenAPI contract is unnecessarily awkward
2. if yes, fix the server/OpenAPI contract directly
3. regenerate
4. if the generator still exposes a generator-specific shape that cannot be improved cleanly at the server boundary, adapt it at Workspace

Never hand-edit generated code and do not introduce a generated-file rewrite/post-processing script.

---

# 1. Scope of 03b

03b completes the HTTP transport migration in the Web application.

03b includes:

1. add `@rtk-query/codegen-openapi`
2. create the minimal handwritten `baseApi`
3. generate one RTK Query client from committed OpenAPI
4. replace all handwritten endpoint definitions in `services/api.ts`
5. replace handwritten request/response DTO duplication with generated DTOs where semantics match
6. update Sales, Trader, Post Process, App/settings, search, grid-layout, and other current consumers
7. preserve existing Workspace -> capability -> Screen composition
8. normalize top-level RTK Query/API errors at Workspace/transport boundaries
9. move handwritten SSE transport out of `App.tsx`
10. remove the intermediate `openapi-typescript` schema-only path
11. make `npm run generate:api` the canonical generation command
12. update tests to cover the new generated/handwritten boundary
13. leave the repository with no production import from the old handwritten `services/api.ts`

03b does **not** include:

- new business workflows
- persistence/serialization cleanup
- ID-model redesign
- Event ID allocation/order redesign
- Event Business Date redesign
- CI setup
- horizontal scaling / LISTEN-NOTIFY
- a new client-side business cache synchronization policy
- a broad page/domain refactor unrelated to transport migration

---

# 2. Target Web transport structure

Use this target shape:

```text
src/Rfq.Web/src/
  services/
    baseApi.ts
    worklistStream.ts
    [small shared API-problem helper only if useful]

  generated/
    rfqApi.ts

  app/
  pages/
  shared/
```

Delete after migration:

```text
src/Rfq.Web/src/services/api.ts
src/Rfq.Web/src/generated/api-schema.ts
```

Do not replace `services/api.ts` with another handwritten endpoint catalog.

---

# 3. `baseApi.ts`

Create one handwritten RTK Query base API.

It owns only transport-global HTTP setup:

- `createApi`
- `fetchBaseQuery`
- reducer path
- base URL `/api`
- current development identity header propagation through `X-Development-User`
- credentials or other genuinely global HTTP settings if already required

Conceptually:

```text
baseApi
  baseQuery = fetchBaseQuery(...)
  endpoints = () => ({})
```

It must **not** own:

- RFQ endpoints
- request/response DTOs
- RFQ business transforms
- Workspace reconciliation
- Live/Paused state
- SSE
- OpenAPI-tag-derived cache invalidation
- page-specific retry/error behavior

Do not define `tagTypes` merely to reproduce the old handwritten API cache policy.

The Redux store may use `baseApi.reducer`, `baseApi.reducerPath`, and `baseApi.middleware`; generated endpoints are injected into the same API instance.

---

# 4. RTK Query OpenAPI generation

Use:

```text
@rtk-query/codegen-openapi
```

The committed server OpenAPI remains:

```text
artifacts/openapi/Rfq.Api.json
```

Generate one file:

```text
src/Rfq.Web/src/generated/rfqApi.ts
```

Use the handwritten `baseApi` as the generator's API input/import.

Generate:

- query hooks
- lazy query hooks
- mutation hooks

All current generated operation names must derive from the explicit 03a `operationId` values.

Do not invent a second endpoint naming scheme in the Web layer.

## 4.1 Tags

OpenAPI tags may remain in the OpenAPI document for classification/documentation.

For RTK Query generation:

```text
tag: false
```

or the equivalent configuration that emits **no tag-derived** `providesTags` / `invalidatesTags`.

Do not reproduce the handwritten `PostProcess`, `QuoteExpiry`, `QuoteMode`, `Theme`, or `GridConfig` tag scheme merely because it exists today.

This application already has explicit authoritative reconciliation. Generated cache tags must not become a second state-synchronization policy.

Settings/grid save flows may explicitly use returned values or explicit refetch where the current UX requires it; do not build a general cache invalidation system for them in this task.

## 4.2 One generated file

Generate one client file initially.

Do not split by OpenAPI tag, controller, or page.

Generated source is not the primary human-maintained organization boundary. Split only later if actual size/build/ownership pressure justifies it.

## 4.3 Generated source is read-only

Never manually edit:

```text
src/Rfq.Web/src/generated/rfqApi.ts
```

Do not patch it after generation.

If generated output is poor:

1. improve server/OpenAPI when that is the real source of the problem
2. otherwise adapt at Workspace

Do not add custom AST/text rewriting of the generated file.

---

# 5. Generation tooling

Update the existing canonical command:

```text
npm run generate:api
```

It must perform the complete current generation flow:

```text
dotnet build / OpenAPI generation
→ RTK Query OpenAPI generation
→ format generated output/config as needed
```

The command should work from the repository's documented/current development environment without manual file edits.

Remove the old `openapi-typescript` generation step and dependency when nothing else uses it.

Remove:

```text
src/Rfq.Web/src/generated/api-schema.ts
```

after all consumers are migrated.

Do **not** add a separate drift-check command in 03b.

There is no CI pipeline yet. A meaningful drift check would regenerate first anyway. When CI is introduced later, it can run generation followed by a diff check.

As an acceptance check for 03b, rerun `npm run generate:api` after the repository is otherwise clean and confirm that a second identical generation produces no unexpected diff.

---

# 6. Generated DTO policy

Generated request/response DTOs may be used directly through Workspace and into Screen when:

- the API and UI concept mean the same thing
- the lifecycle is the same
- no UI-only state is being mixed into the DTO

Do not create duplicate page DTOs solely to hide generated types.

Examples that can normally remain generated transport data:

- Sales RFQ worklist row
- Trader RFQ worklist row
- Case operation result
- quote payload
- search result
- reference-data result
- settings response
- grid-config response
- Recent Revisions response

Create page-local types only when the UI concept genuinely differs, for example:

- transient editor state
- staged input
- selection state
- derived display model
- page-only command abstraction

Do not mutate/extend generated DTO objects to store UI interaction state.

---

# 7. Workspace and Screen boundary

Preserve the existing overall composition:

```text
generated hook
    ↓
Workspace
    ↓
page capability interface
    ↓
Screen
```

But interpret this as a **behavioral transport boundary**, not a rule that every API DTO must be copied.

Workspace owns:

- generated hook invocation
- `unwrap()`
- construction of generated request arguments
- one-item calls to plural endpoints
- mutation reconciliation
- top-level API/transport failure normalization
- adaptation only where the page concept differs

Screen may consume generated DTO shapes directly.

Do not add pass-through adapters whose only behavior is:

```text
generated type A -> identical handwritten type B
```

---

# 8. Error boundary follow-up

RTK Query-specific error shapes must not cross Workspace.

In particular, Screen must not need to know about:

```text
FetchBaseQueryError
SerializedError
{ data: ... } RTK rejection envelopes
```

When `.unwrap()` rejects:

- if the server returned the stable `ApiProblemDetails` contract, expose that problem object to the page capability/Screen
- if there is no valid server problem body because of network/parse/transport failure, reduce it to a generic transport failure suitable for the capability
- do not create another global `OperationError` DTO merely to rename `ApiProblemDetails`

Preserve the semantic distinction:

```text
CaseOperationResult.Status == Failed
    -> ordinary HTTP-200 business item result

unwrap() rejects
    -> top-level API / transport failure
```

Current Screen behavior that depends on meaningful server information may continue to do so after normalization.

Examples:

- Trader can display `ApiProblemDetails.detail`
- Sales amendment autosave may distinguish `ApiProblemDetails.status == 409`

But Screen must inspect the normalized problem itself, not RTK Query's rejection wrapper.

A small shared helper under `services/` is acceptable if it centralizes this extraction without creating a new application error hierarchy.

---

# 9. SSE transport follow-up

SSE is not generated by the OpenAPI RTK Query client.

Move handwritten EventSource plumbing from `App.tsx` to:

```text
src/Rfq.Web/src/services/worklistStream.ts
```

The transport module owns:

- final `/api/worklists/stream` URL construction
- development identity query parameter propagation
- `EventSource` creation
- invalidation event parsing
- reconnect behavior inherent to EventSource
- close/dispose
- callback delivery

Preserve the 03a development identity semantics:

```text
normal HTTP:
  X-Development-User header

EventSource:
  developmentUser query parameter

server:
  header takes precedence over query fallback
```

Do not generalize this into production authentication design.

`App.tsx` owns only interpretation such as:

```text
sales-list
  -> increment salesChangeVersion

trader-list
  -> increment traderChangeVersion

recent-revisions
  -> increment recentRevisionsChangeVersion

business-date
  -> refetch Business Date + wake relevant worklists
```

SSE remains wake-up only.

Do not add Event payload row-patching.

---

# 10. Consumer migration

Migrate all current imports from:

```text
@/services/api
```

to the generated client and/or small handwritten transport modules.

This includes at least:

- `App.tsx`
- Redux store
- Sales Workspace/contracts/helpers
- Trader Workspace/contracts/helpers
- Post Process
- grid settings
- personal settings
- search
- reference-data lookup
- tests

Prefer generated exported types over handwritten copies.

The generated code may choose names slightly different from the old handwritten aliases. Update the application to the generated contract rather than recreating old aliases solely for compatibility.

Where a concise page-level semantic alias genuinely improves readability, a TypeScript alias is fine, but it must not duplicate the full shape.

---

# 11. Preserve current RFQ synchronization behavior

Generated RTK Query migration must not alter the 02b/03a authoritative state model.

Preserve:

- current-Business-Date snapshot authority
- relevant SSE wake-up
- Live/Paused semantics
- explicit authoritative `refetch`
- per-Case reconciliation after mutations
- mutation-success vs reconciliation-read-failure distinction
- Recent Revisions lazy/stale behavior
- Post Process explicit query reconciliation
- CaseOperationResult partial success semantics

Do not add optimistic business-state patching from mutation responses merely because generated hooks expose returned data.

Do not use generated cache invalidation as a substitute for these behaviors.

---

# 12. Backend/OpenAPI changes allowed in 03b

The 03a contract is accepted.

Do not casually rename routes, use cases, DTO semantics, or operationIds.

A small backend/OpenAPI correction is allowed only when generated-client work demonstrates that the OpenAPI contract itself is unnecessarily malformed or ambiguous.

If such a correction is required:

- keep it minimal
- update the corresponding API/OpenAPI contract test
- regenerate the committed OpenAPI
- do not preserve the old private contract for compatibility
- do not add a frontend post-processing workaround when the server contract is clearly the problem

If a generator limitation remains after a clean server contract, adapt it at Workspace instead.

---

# 13. Tests

Keep the existing backend 03a contract tests.

Update/add Web tests for the new boundary.

At minimum cover:

1. development identity header still comes from `baseApi`
2. representative generated query uses the expected request contract
3. representative generated mutation uses the expected request contract
4. one-item UI actions still call plural APIs correctly
5. `CaseOperationResult.Failed` remains a business result
6. a server `ApiProblemDetails` rejection is normalized before Screen
7. an unknown network/transport rejection does not expose RTK Query-specific shape to Screen
8. worklist SSE URL carries the development identity query parameter
9. SSE invalidation categories are parsed/delivered correctly
10. existing Sales/Trader/Post Process page behavior remains green

Do not unit-test generated source implementation details line-by-line.

The OpenAPI contract tests already protect operationIds/routes/schema-level contract; the generated-client tests should protect integration at the handwritten boundary.

---

# 14. Cleanup requirements

At the end of 03b:

- no production code imports `@/services/api`
- `services/api.ts` is deleted
- `generated/api-schema.ts` is deleted
- `openapi-typescript` is removed if unused
- `@rtk-query/codegen-openapi` is installed/pinned through the package lock
- `generated/rfqApi.ts` is committed
- no generated tag invalidation policy exists
- no handwritten duplicate endpoint catalog exists
- `App.tsx` does not construct/manage EventSource directly
- Screen does not inspect RTK Query rejection envelopes
- generated source has not been manually edited
- generated DTO duplication is removed where no semantic adaptation exists

Use search to prove the important absences rather than assuming cleanup is complete.

---

# 15. Validation

Run the relevant repository checks after migration.

At minimum:

```text
dotnet build
dotnet test
```

and in `src/Rfq.Web`:

```text
npm run generate:api
npm run build
npm run lint
npm run test -- --run
```

Use the repository's current equivalent command if one of these scripts differs.

Then rerun:

```text
npm run generate:api
```

from a clean post-generation state and confirm there is no unexpected generated diff.

Do not add CI solely for this task.

---

# 16. Deliverable

Commit the complete 03b implementation.

In the final report include:

1. commit SHA
2. generated-client/config/baseApi files introduced
3. handwritten transport/schema files removed
4. any server/OpenAPI corrections required for generation
5. where SSE transport now lives
6. how top-level API errors are normalized before Screen
7. tests/commands executed and results
8. confirmation that a second generation produced no unexpected diff

If all requirements above are satisfied, 03b closes the current 03 series. Do not invent a 03c cleanup phase for residue that belongs in this task.
