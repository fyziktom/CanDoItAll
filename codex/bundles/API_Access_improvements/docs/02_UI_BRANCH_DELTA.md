# 2. Selective integration of ui-refactoring-v2

**Reference:** UI head `e101d5db1478ea329a572db79c0104b927d97f15`, common ancestor `a2903c400cc35e6d1d2f233c51e73feb256ce2aa`. Paths below are relative to the main repository. Evidence IDs refer to the sources document.

## 2.1 What the colleague actually added

The relevant branch delta is primarily API coverage and response metadata, not a completed JWT/user subsystem. The inspected access routes only add response declarations to pre-existing status and issuance operations. There is no new login/account implementation or independent access-administration gate in those changes. [U01, U02]

The substantive endpoint additions identified are eight method/route pairs:

| Method and route | UI-branch implementation | Integration decision |
|---|---|---|
| `GET /api/processes/definitions` | Catalog projection; `searchText`, optional scope filter, `{ items: [...] }`. | Preserve intent and client shape; use current process application/projection owner. |
| `GET /api/processes/definitions/{definitionKey}` | Definition editor/overview projection. | Preserve opaque definition keys and safe not-found behavior. |
| `GET /api/processes/definitions/{definitionKey}/roles` | Role-editor projection. | Read capability; do not accidentally expose write permission. |
| `GET /api/processes/definitions/{definitionKey}/steps` | Step-editor projection. | Read capability; keep existing domain filtering. |
| `GET /api/workflows/templates` | Template-pack catalog with counts, backend and a display flow summary. | Add only if not already present at the actual development HEAD. |
| `POST /api/workflows/templates/{templateKey}/drafts` | Creates an LLM component, then a draft workflow using a selected provider/model. | Move orchestration to the owning application service; protect consistency and existing approvals. |
| `GET /api/settings/workspace` | Returns `WorkspaceSettingsModel`. | Add to an API settings family with explicit scope and main API gate. |
| `PUT /api/settings/workspace` | Saves workspace defaults and reads them back. | Preserve read-back semantics, but protect mutation and validation. |

The new settings routes in the UI branch are mapped directly on `app` in `Program.cs` after `MapCanDoItAllApi()`, with no local `.RequireAuthorization(...)` and no surrounding API-enabled condition. Their `/api` path does **not** make them children of the protected route group. This is a concrete mapping problem, not an assumption that a proxy will always compensate for it. Do not port this wiring. [U05]

## 2.2 File-level integration map

| Changed source | Useful delta | What not to bring back |
|---|---|---|
| `src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs` | Typed status/token responses. | Older routing composition, absence of newer shared-provider and storage-recovery mappings, or the old insufficient exposure model. Development already has richer declarations here. |
| `.../Api/ProcessDefinitionsApi.cs` | Four process-definition read endpoints. | Blanket `InvalidOperationException` → 404, or an unconditional global workspace assumption without checking the current owner. |
| `.../Api/ProcessesApi.cs` | Wires definition endpoints and advertises their contract. | Older process dispatch/run-record behavior or less complete diagnostics. |
| `.../Api/ProjectsApi.cs` | Typed response/error metadata. | Overwriting newer named handlers, validation or ownership behavior just to obtain annotations. |
| `.../Api/PromptGalleryApi.cs` | Typed response/error metadata. | Assuming declared response types are correct without checking what `FromResult` actually emits. |
| `.../Api/WorkflowsApi.cs` | Templates, draft creation and metadata. | Replacing development's run-read, external-response, idempotency or governance hardening with the older file. |
| `src/App/CanDoItAll.Web/Program.cs` | Workspace-settings routes; typed runtime capability response. | Direct unguarded settings mapping, old diagnostics placement, or UI-specific AppTheme/AppToolbar registrations. |
| `src/App/CanDoItAll.Web/ProjectStructureAgentApi.cs` | Response declarations and a named acknowledgement shape instead of anonymous output. | Changing actual JSON casing/shape, stripping lease/caller checks, or moving current owners back into a monolith. |

This map derives from the common-base comparison and the latest UI commit's patches. It is not an instruction to cherry-pick the whole commit. The remaining branch changes include the alternative UI and unrelated presentation/dependency edits, which are outside this handoff. [U01]

## 2.3 Process-definition reads

The colleague calls `ProcessDefinitionCatalogProjectionService`, `ProcessDefinitionEditorProjectionService`, `ProcessDefinitionRoleEditorProjectionService` and `ProcessDefinitionStepEditorProjectionService`, using `ProcessWorkspaceShellScope.Global` and `ProcessDefinitionCatalogItemKey`. [U03]

Before porting, resolve the current implementations and their project dependencies. A projection service in the process application layer can be reused; a Web dependency on a Razor editor or UI host merely to obtain data is not acceptable. If current owners already provide equivalent contracts, adapt the HTTP layer instead of duplicating projections.

Preserve the distinction between a missing definition and a real persistence/runtime failure. Catching every `InvalidOperationException` and returning “not found” hides operational faults. Prefer an existing typed result or a precise not-found condition. Validate search/filter values and test invalid enum input. Definition keys are not assumed to be GUIDs; exercise actual seeded key formats and URL encoding.

The branch exposes read projections only. Do not promise process-definition CRUD that these additions do not implement. Adding unrelated process editors is out of scope.

## 2.4 Workflow templates and draft creation

The listing loads `WorkflowTemplatePackLoader` and produces each template's key/name/description, graph node/edge counts, input parameter count, preferred backend and a summary grouped by node kind. That summary is display text, not a serialized executable graph. Keep its documented meaning stable. [U04]

The branch's draft action resolves a template case-insensitively, picks a draft name, chooses an enabled structured-output provider where possible, falls back to other enabled providers, creates an `LlmCallComponent`, constructs a template-based draft and saves the definition. It uses model fallbacks and explicitly sets several permission flags, including `RequiresApprovalForExternalCalls=false`. [U04]

Do not copy this as an HTTP lambda containing a second implementation of template instantiation. Find the current template-to-draft service used by the shipped UI, or introduce the smallest suitable application-level operation if none exists. The API and existing UI should have one consistent owner.

Required safeguards:

- Validate template, provider/model availability and the complete draft before committing dependent writes. Do not silently pick a disabled provider or assert structured-output support that is absent.
- Ensure failure while saving the definition does not strand a new component. Use the existing transaction boundary where the owners share one, or a narrow, explicit compensation/cleanup strategy that only touches records created by this operation. Do not introduce a distributed transaction platform.
- Do not report failure after a successful durable write merely because subsequent presentation refresh failed. Preserve existing mutation-result/read-back semantics.
- Draft creation must not execute a model request, start a workflow, charge an external provider or weaken approval/tool policy. Any deliberate draft defaults must match the current template owner, not the older API lambda.
- Concurrent names/repeated clicks must not overwrite another definition. Define whether a repeat creates a second draft or is idempotent; reuse existing operation/idempotency infrastructure where it fits. Do not invent a new global idempotency subsystem for this endpoint.
- Test unknown template, unavailable provider, failing second write, concurrent creation and cross-section rejection. Validate stored draft/component state, not just status 200.

## 2.5 Workspace settings are not security configuration

The inspected `WorkspaceSettingsModel` contains workspace name, default provider profile, default output format, currency code/culture and notes. The current service uses `WorkspaceSettingsDbContext`; it updates currency display state and records an activity after saving. This is workspace business configuration, not the JWT signing-key configuration or the proposed API-user store. [D15]

Use a dedicated API DTO or carefully bounded reuse of this exact safe model. Preserve normalization and provider validation conventions. Avoid a generic “settings object” accepting arbitrary keys. Add `api.settings.workspace.read` and `.write` or equivalent named catalog entries, and test read-only users cannot perform `PUT`.

These business settings routes are controlled by the business API flag and their scopes. They are **not** access administration and must not gain the ability to alter users, signing secrets or the administrative exposure switch. Conversely, an administrator identity need not implicitly bypass every workspace-data permission.

## 2.6 OpenAPI is a contract, not just extra annotations

The colleague added numerous `.Produces<T>()` calls because the API-only UI needs dependable types. Development already has richer XML/typed declarations in several touched files. Retain the current correct declarations and fill actual gaps. In particular, check the acknowledgement bodies emitted through `ApiEndpointResults.FromResult`, not just what a proposed `.Produces<ApiAck>()` says. [U01, D03, D11]

Use real responses and the generated OpenAPI document to verify:

- method/path and operation ID uniqueness, request/response shape, required/null fields and enum behavior;
- anonymous login/status operations versus secured operations; bearer requirements do not apply to the login request itself;
- 401, 403, 404, validation errors and disabled-surface behavior;
- absence of sensitive DTO fields and secret-bearing example values;
- no duplicate route mapping or stale public contract lists;
- configured-off administration is not advertised as callable. Login/users support is discoverable without exposing account inventory anonymously.

Do not import the alternative UI into development just to prove these contracts. A small browser test client plus the shipped Settings page is sufficient for this handoff; an existing API-only UI build can be used when already available and relevant.
