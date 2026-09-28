# CanDoItAll API access — combined implementation handoff

Prepared 20 September 2026. This generated file combines the prompt and supporting documents. The individual files in the package are the canonical editable copies. Repeated descriptions express the same requirements, not additional scope. Configuration examples and the optional Git utility remain separate files.

**Application verification status: NOT RUN. This is an implementation handoff, not an implemented feature.**


---

<!-- Source: CODEX_PROMPT.md -->

# Implement API access users and selected API-only UI support

You are the implementing senior C#/.NET architect and engineer. Work in the main CanDoItAll repository on the current `development` line in preparation for a later development-to-main merge. Complete the implementation, focused repairs, real API validation and Settings browser validation in one sustained task.

This is **not** a CanDoItAll workflow bundle. Do not create bundle scaffolding, subbundle gates, artificial approval checkpoints or a plan-only deliverable. Maintain a short working plan and evidence log, make coherent implementation steps, and continue until the acceptance criteria are satisfied or a concrete environmental blocker is documented. You may improve internal decomposition and names where the current code warrants it, but preserve the security and scope boundaries below.

## Repository orientation

Read the repository's current `AGENTS.md`, `.github/copilot-instructions.md`, `docs/testing.md`, `.github/workflows/ci.yml` and `docs/architecture/ui-component-seams.md`. Use the applicable SharedInfo standards and `candoitall-codeanalytics-mcp` skill. These standards apply; their bundle ceremony does not.

The source analysis accompanying this prompt is pinned to:

- development: `b82ffc57283f5e4819d82322e1c5bf836dcd9536`;
- UI reference: `e101d5db1478ea329a572db79c0104b927d97f15`;
- merge base: `a2903c400cc35e6d1d2f233c51e73feb256ce2aa`.

Verify the actual local HEAD, branch, working tree and sibling dependencies first. Preserve unrelated work. If development advanced, reconcile this handoff with the newer implementation; do not reset it to the reviewed SHA. Treat the UI branch as a read-only reference. Compare its changes against the common ancestor, then check whether development already implements each behavior. A direct development-versus-UI diff is not a ready-to-apply patch.

Read all supporting documents in this directory before choosing the implementation details. Source paths and symbols are starting points, not permission to reintroduce an obsolete owner or dependency.

## Product objective

An API-only UI will access a CanDoItAll instance on a server. The existing Blazor SSR UI remains a trusted operator surface on a desktop or protected administrative path. Container deployments expose only explicitly allowed API traffic through a proxy and do not expose SSR, its interactive transport or developer endpoints. This is an internal PC/LAN/VPN application, not a public SaaS identity service.

Implement:

1. Minimal API accounts with stable IDs, username, display name, enabled state, password hash and explicit API permissions. Administrators create, edit, disable, delete and reset passwords. No registration, email delivery, password recovery links or CRM identity integration.
2. A configured administrative identity whose password **hash** comes from deployment configuration/environment. No built-in password and no silent fallback. The supported simple development path is to leave the new surfaces disabled, or supply a developer-owned hash and use the real flow.
3. Login that issues a fresh registered user-session JWT. A user can log in again after expiry without asking an administrator to issue another token. Do not add refresh tokens unless a concrete requirement cannot be met without them; the agreed baseline intentionally does not require them.
4. A separately enabled, strictly administrator-only HTTP surface for managing API users and machine-token metadata/lifecycle, including the existing token issuance route. Neither an open API mode, broad `api`, a claimed role nor an old `api.tokens.issue` token may silently bypass this administrative boundary.
5. Server-side API-section enforcement for users **and** machine tokens. Keep existing explicit privileged scopes and resource authorization checks. A narrowly scoped shared-provider token must not gain access to unrelated workflow/project/settings endpoints merely because its signature is valid.
6. Selective integration of the colleague's useful API additions: process-definition reads, workflow-template listing/draft creation, workspace-settings reads/writes, and missing accurate OpenAPI contracts. Do not port the UI/theme refactor or overwrite development hardening.
7. Extension of Settings → API access to manage users, show effective configuration and continue managing machine tokens. No new application-wide SSR login requirement.

## Security and compatibility invariants

- Preserve existing service/client JWT operation, signing-key/issuer/audience configuration, machine token defaults, registry revocation and shared-provider use. Tightening unintended cross-section access is intentional; silently invalidating all machine credentials is not.
- Distinguish legacy tokens, registered machine tokens and registered user/admin sessions using an unambiguous validation path and authoritative server-side metadata. Never infer a user or administrator from an arbitrary `sub`, `name`, `role` or scope in a machine token.
- Existing accepted legacy tokens without a managed-token marker are not automatically administrators. Do not accidentally remove their legitimate data access or give them new rights. Preserve existing claim mapping unless a compatibility-tested change is necessary.
- Administration via HTTP requires a validated administrative session and the independent host exposure switch. This includes direct calls to administrative application services; a hidden button is not authorization.
- Trusted local UI administration remains a separate server-side capability. It must never authenticate a raw HTTP API request, a forwarded request or an arbitrary background scope as a local operator.
- Password reset, disable, deletion and permission changes invalidate existing affected user sessions on subsequent authorization. Re-enabling an account must not resurrect its old sessions. Do not revoke unrelated machine credentials on a user change.
- User-selected permissions and administrator authority must come from the server, not login JSON. Reject attempts to overpost privileged fields. Empty business permissions must never become broad `api` access.
- No signing key, password hash, token plaintext, secret configuration or credential fingerprint in list/status DTOs, OpenAPI examples, normal logs, screenshots or test reports. Token values are one-time responses with no-store headers.
- Keep bootstrap secrets and exposure switches deployment-owned. Do not provide a generic HTTP configuration writer that can enable its own administrative API or read/replace signing secrets.
- Preserve existing domain ownership/approval checks, sandboxing, provider publication grants and resource-level checks. API accounts are not a new application-wide identity/ownership model.
- Preserve the intentional open business API mode. Clearly show that user scope isolation is not enforced on anonymously open business endpoints; never pretend an open mode is a protected multi-user deployment.
- The new login/admin/settings routes must not accidentally sit outside configuration or policy groups. Verify actual endpoint metadata and HTTP behavior, including error-pipeline re-execution, aliases, streams and companion download routes.

## Architectural latitude

Use the small architecture in `docs/03_ARCHITECTURE.md`. Prefer extending existing ApiAccess application services and the private control-plane persistence pattern rather than creating a new platform. Do not introduce a Foundation → Modules dependency or make shared-provider runtime depend on a Workspace UI component. Transport adapters belong in Web; state and business rules do not belong in Razor handlers.

A single configured administrator plus ordinary persisted API accounts is sufficient for this task. Ordinary account CRUD must not expose administrator promotion. Keep the configured administrator visible as configuration-owned and recoverable by the deployment operator. Multiple administrative roles, SSO and EGCP are future work.

The proposed names/routes/options may be adjusted to fit repository conventions. Preserve the user-visible behavior and record deliberate deviations, especially any client contract difference from `ui-refactoring-v2`. Do not broaden the task into unrelated module extraction or a full authentication rewrite.

## Efficient verification

Use `candoitall_codeanalytics` and its canonical `code_analytics_impacted_tests_get` tool to select tests from **actual changed paths and line ranges**. The applicable skill in SharedInfo is `codex/skills/candoitall-codeanalytics-mcp/SKILL.md`.

The product solution intentionally contains no tests. Supply the appropriate separate test workspaces. Use `changes` only for actual edits and `contextOnlyPaths` for inspected files. This task changes authorization behavior and contracts: use `BehaviorChange`, `ContractOrShapeChange` or the initial conservative `Unknown`, not a false behavior-preserving assertion. Inspect workspace health, unresolved ranges, required selectors, conditional selectors and promotion triggers. Correct missing/zero discovery; do not treat it as no impact.

Build changed production projects and refreshed owning test assemblies, discover the selected tests, record expected/actual counts, then execute the narrow selectors. Re-query impact after a coherent change expands the diff. Do not run every suite after each edit. Equally, do not discard a wider required selector merely because it is inconvenient. At final closure consider the named auth/DI/contract changes as explicit reasons for a broader gate if required by repository guidance, rather than doing repeated unfiltered runs.

Run the mandatory `portability-static` procedure and final enforcement without `--write-baseline`; reviewed baseline updates are not a substitute for repairing real findings. Use the repository's current commands, not guessed scripts.

Unit tests, fake principals and component mocks are not final security proof. Execute the real JWT handler and HTTP pipeline, then a running application and Playwright Settings journeys. Use isolated test data, the real private account/token persistence and synthetic or local provider fixtures. No unapproved paid LLM calls. Cover the positive/negative cases in `docs/06_VALIDATION.md`, including administrator bootstrap, account changes, token expiry/revocation, section isolation, disabled surfaces, legacy clients, proxy/local-operator separation and browser mutations.

The existing integration harness can support fast iterations with its documented test-only database options. Final deployment proof must also exercise the production composition and its real supported persistence, not only a replacement host with a fake authentication scheme. Report blocked PostgreSQL/container/browser lanes honestly; do not substitute mocks and claim closure.

## Working tree, commits and language

Keep code, code comments, UI text, documentation, tests, examples, filenames and commit messages in English. A final conversation summary may be in Czech. Do not add Radzen; use the current shared components and, where useful, the Components MCP.

Commit coherent validated work according to repository policy, preserving GPG signing. Reuse the existing unlocked GPG agent and compatible shell/session where possible. Do not disable signing, export keys, save passphrases, alter global GPG cache/security settings, or rewrite signed history. If signing cannot complete, leave the work intact and report that blocker instead of creating unsigned commits. Do not merge development into main, push, open a PR or modify the colleague's branch without separate authorization.

## Required final deliverable

Deliver working code and maintained operator/API documentation, not only a proposed design. Include a concise implementation report with:

- actual base/final HEAD, changed owners and signed commits or the precise signing blocker;
- what was integrated from each UI-branch API delta and what was already present;
- the configuration truth table and explicit compatibility/security changes;
- the source of administrator credentials and the documented hash-generation/setup flow;
- the actual affected-test discovery and execution results, mandatory static result, real HTTP and browser evidence, and persistence/restart proof;
- all failed, skipped, unavailable or deferred checks, clearly separated from passing ones;
- residual limitations, with no claim that the development-to-main merge has been performed or that the whole application is internet-hardened.

Keep working through ordinary failures and repair them. Only concrete environmental blockers justify incomplete lanes. Never label a task complete solely because the compiler passes or the browser renders the settings form.


---

<!-- Source: README.md -->

# CanDoItAll: API access and API-only UI integration

**Implementation handoff for Codex gpt-6 Extra — 20 September 2026**

## Start here

Give Codex `CODEX_PROMPT.md` and this directory. The prompt defines one autonomous engineering task, not a workflow bundle. The supporting documents distinguish observed repository behavior from proposed changes.

Read in this order:

1. `CODEX_PROMPT.md` — execution instructions and non-negotiable boundaries.
2. `docs/01_CURRENT_IMPLEMENTATION.md` and `docs/02_UI_BRANCH_DELTA.md` — pinned source review and selective integration map.
3. `docs/03_ARCHITECTURE.md` and `docs/04_API_AND_CONFIGURATION.md` — target design, compatibility, configuration and HTTP contracts.
4. `docs/05_SETTINGS_UI.md` and `docs/06_VALIDATION.md` — operator experience and executable acceptance criteria.
5. `docs/07_SOURCES_AND_LIMITS.md` — evidence index, external references and review limitations.

`ALL_IN_ONE.md` combines these documents for tools that prefer one input file. It is generated from the individual documents; do not treat both copies as separate requirements.

## Reviewed source snapshot

| Repository/ref | Reviewed commit |
|---|---|
| `fyziktom/CanDoItAll`, `development` | `b82ffc57283f5e4819d82322e1c5bf836dcd9536` |
| `fyziktom/CanDoItAll`, `ui-refactoring-v2` | `e101d5db1478ea329a572db79c0104b927d97f15` |
| Common ancestor | `a2903c400cc35e6d1d2f233c51e73feb256ce2aa` |
| SharedInfo CodeAnalytics guidance | `776a6a329dce5764ba07f6203a8d2ed103d39c2b` |

At this snapshot the UI branch is 35 commits ahead of, and 223 commits behind, development. The latest UI commit is `api spec fixes`. Do not merge or overwrite whole files from that older branch. Reimplement the useful deltas against current development owners and contracts.

## Decision in one paragraph

Keep the current machine JWT capability and trusted local Blazor operator experience. Add small instance-local API accounts, an environment-configured administrative login, and registered user-session JWTs. Keep API exposure, user login exposure, and HTTP access administration separate. Enforce the selected API sections at the endpoints, not merely in the account editor. Do not add registration, global SSR authentication, tenants, a new ownership model, a full identity platform or refresh-token infrastructure to this change.

## What this package is and is not

This is a source-backed architecture and implementation handoff. No CanDoItAll source was changed, no branches were merged, and no CanDoItAll builds, HTTP requests against a running host, or browser tests were executed during preparation. All application validation in the acceptance matrix is **NOT RUN** until Codex implements and executes it.

The optional `tools/capture_review_snapshot.py` creates read-only Git comparison evidence from an existing local checkout. It does not fetch, checkout, commit, merge or run application tests. `examples/*.json` are proposed configuration/contract examples, not files already supported by the reviewed application.


---

<!-- Source: docs/01_CURRENT_IMPLEMENTATION.md -->

# 1. Current implementation and gaps

**Status:** source inspection of the pinned development commit; not runtime penetration testing. Evidence IDs refer to `07_SOURCES_AND_LIMITS.md`.

## 1.1 Existing building blocks worth preserving

`ApiAccessOptions` in Workspace/ApiAccess already separates business API mapping, OpenAPI and Swagger switches from `Api:Authorization:Enabled`. The reviewed defaults are API/OpenAPI/Swagger enabled and JWT authorization disabled. The signing key must contain at least 32 UTF-8 bytes when authorization is enabled. The default machine-token lifetime is 480 minutes with a 1440-minute maximum. A minimum byte count is not proof of key entropy: deployment instructions should generate a cryptographically random key, not a predictable 32-character phrase. [D01]

The issuer produces HS256 JWTs with issuer, audience, subject, display name, timestamps, a `jti`, `cda_token_version=1`, and both a space-delimited `scope` and a `scopes` collection. It registers metadata before returning the token. Listing records cannot retrieve the original bearer value. Preserve these machine-token semantics unless a separately justified, tested change is needed. [D01]

`ApiServiceCollectionExtensions` installs JWT Bearer validation of issuer, audience, signature and lifetime, with 30 seconds of clock skew. Its validation event checks the managed-token registry. Authentication registration and the host authentication/authorization middleware are conditional on authorization being enabled. This must be taken into account when adding login and admin options; adding endpoint metadata without the correct configured pipeline is insufficient. [D02, D10]

The registry is not a business database table. `FileApiTokenRegistry` stores one versioned JSON record per token in `api-tokens` under `IControlPlanePathResolver`. It uses `DurableFileWriter.Private`, create-new semantics for issuance, per-token mutation coordination, and private directory creation. The current record validator rejects empty scopes. This is the natural persistence pattern for small instance-local API users, without adding an EF Identity schema or coupling login to the selected workspace database. [D04]

`ApiTokenAdministrationService` already protects issuance, search, revoke and delete through `IApiTokenAdministrationAccess`. The Web adapter accepts a trusted interactive local-operator principal or an authenticated bearer with `api.tokens.issue`. The `/api/access/tokens` HTTP endpoint currently calls the lower issuer directly and uses its endpoint scope policy rather than that administration service. New HTTP routes should not create a second, weaker administrative path. [D03, D05]

Settings already contains the `api-access` tab, `ApiTokenAdministrationPanel`, `ApiScopePickerDialog` and `ApiTokensDialog`. The panel supports one-time token display and metadata management. Reuse these capabilities; add user administration and configuration state instead of replacing the Settings experience. [D08, D09]

## 1.2 Distinguish three existing boundaries

**Authentication:** is the bearer cryptographically valid and, when marked as managed, still registered and active?

**API permission:** does it have the exact required scope or the permitted broad `api` alternative for this endpoint?

**Domain permission:** does the operation's existing resource/owner/approval policy permit this actor and effect?

The new work must complete the second boundary and add account liveness to the first. It must not replace the third with an administrator-wide bypass.

## 1.3 Findings and implementation consequences

### F01 — Sensitive HTTP token issuance has no independent exposure switch

`POST /api/access/tokens` is mapped whenever the main API is mapped. In secure mode it requires `api.tokens.issue`; broad `api` alone is insufficient. In open mode the route is mapped without that scope policy, but the issuer rejects the operation because JWT authorization is disabled. The observed open-mode behavior is therefore a 400, **not anonymous successful token issuance**. [D01, D03, D12]

Required change: make all HTTP access-administration routes independently opt-in and admin-only. With the new gate closed the routes should be absent/404, including the existing issuance route. Keep direct trusted-operator token administration available in secure local mode even when its HTTP equivalent is disabled.

### F02 — A valid narrow bearer can still pass general API families

The `/api` parent group requires authentication, not the `GeneralApi` scope policy. For example, the reviewed workflow family explicitly documents that any valid bearer can use its general endpoints. Other families need inventory at implementation time. Existing policies on memory, chats, project structure, provider history and shared providers have different explicit/broad semantics. [D02, D03, D06, D11]

Required change: each ordinary API operation needs an explicit section decision. Merely storing a selected section list in a user or JWT is not enforcement. Do not put `RequireAuthorization(GeneralApi)` on the parent group: policies combine, so that would also reject legitimate narrowly scoped callers to child endpoints. Use appropriate per-family/per-operation metadata and policy composition.

### F03 — Unmarked legacy JWTs deliberately bypass the registry lookup

`ApiManagedTokenValidation` returns without registry lookup when `cda_token_version` is absent. Such a token still has to pass JWT signature/issuer/audience/lifetime validation. For managed v1, unsupported version, invalid token ID, missing/revoked/expired/deleted record or registry failure denies authentication. [D07]

Required change: retain a deliberate legacy path, but never treat a legacy/machine token as a user or administrator. New user sessions require a typed record and current account validation. Do not describe legacy tokens as individually revocable through a registry they never entered. Their current expiry/signing-key lifecycle is a separate compatibility limitation.

### F04 — The current scope parser is not a permission catalog validator

Issuance trims and deduplicates scopes case-insensitively; runtime scope matching is ordinal and case-sensitive. Issuance does not validate known scope names. Explicit null subject/display name or null list elements can reach `.Trim()` and fail outside the intended validation exception handling. [D01, D06]

Required change: one catalog supplies canonical scope names, privilege classification and selectable groups. New writes normalize to canonical known entries and reject malformed/unknown/privileged overposting. Preserve old token interpretation, rather than retrospectively changing case sensitivity or treating unknown old scopes as broad access.

### F05 — Local operator trust already has a meaningful defensive boundary

`LocalOperatorAuthenticationStateProvider` has an internal authentication type and uses both the original socket peer address and effective address. It captures trust for an interactive circuit; a missing HTTP context is not trusted. It does not replace the visible SSR authentication state with a global login policy. The development host captures the original peer before forwarded-header processing in all environments. [D10, D13]

Required change: preserve this boundary and test it. Do not implement new administration as `HttpContext is null => admin`, `loopback => admin` for HTTP, an HTTP header saying “local”, or a catch-all ambient service principal. Local UI and HTTP identities need explicit adapters, not a fallback that elevates an uninitialized scope.

### F06 — Forwarded-header trust is too broad to become a new security assumption

The host clears `KnownIPNetworks` and `KnownProxies`. Its original-peer check partly protects the existing interactive trust path, but new rate limits, TLS decisions or host-based checks must not assume forwarded headers are authentic from arbitrary peers. [D10, S04]

Required change: support explicit trusted proxy configuration and preserve original-peer capture. Separate trusted proxy transport from trusted operator UI addresses. Do not trust an entire VPN/subnet as an operator merely because clients are allowed to reach the API. Test direct and proxied paths, including forged forwarding headers.

### F07 — `Api:Enabled` is not currently a universal host HTTP kill switch

Main API mapping respects `Api:Enabled`. The reviewed status documentation explicitly notes exceptions for project-structure/runtime routes; documentation mapping itself checks OpenAPI configuration separately. Program also maps managed files and the SSR host separately. [D01, D03, D10]

Required change: every **new** login/admin/settings route obeys the appropriate API switch and cannot become another exception. Inventory and document existing exceptions instead of silently promising that `Api:Enabled=false` removes every route. If a stricter global switch is introduced, preserve internal runtime callers or provide a deliberate migration; it is not an incidental auth refactor.

### F08 — Correct errors need the complete production pipeline

The host uses status-code re-execution into `/not-found` and an exception handler targeting `/Error`. An isolated handler test does not prove that disabled routes or API failures remain useful API responses. [D10]

Required change: API failures must retain their 401/403/404/429/503 status and a safe API body rather than becoming successful SSR content or redirects. Tests should disable client auto-redirects and check content type, status, body and absence of writes.

## 1.4 Existing scopes to preserve

| Existing scope | Compatibility note |
|---|---|
| `api` | Broad ordinary API access only where explicitly supported; not universal administrator authority. |
| `api.tokens.issue` | Existing privileged issuance grant. New remote access administration additionally requires the new gate and administrative session. |
| `api.memory-providers.read`, `.write`, `.query` | Preserve the existing broad-or-specific semantics. |
| `api.project-structure.write` | Preserve lease/caller binding and existing checks, including reads historically grouped under this capability. |
| `api.llm-chats.read`, `.manage`, `.execute` | Keep distinct existing exact permissions. |
| `api.workflows.respond` | Preserve exact human-response privilege and caller/owner checks. |
| `api.shared-providers.catalog.read`, `api.shared-providers.invoke` | Preserve actual client/server-provider operation and publication policy. |
| `api.provider-history.read`, `.content.read`, `.manage` | Keep metadata/content/policy distinctions and canonical-owner checks. |
| `api.storage-placement-recovery.read`, `.reconcile`, `.verify-external-termination` | Keep stronger storage recovery authority and project permissions. |

Names above are observed, not a proposal. New section scopes should be explicit additions, not renames of existing ones. [D14]


---

<!-- Source: docs/02_UI_BRANCH_DELTA.md -->

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


---

<!-- Source: docs/03_ARCHITECTURE.md -->

# 3. Target architecture and bounded decisions

Everything in this document is a **proposal for implementation**, unless explicitly described as an existing behavior. Adjust internal names to current repository conventions; preserve the security semantics.

## 3.1 Scope and threat model

The instance runs on a PC or behind a controlled corporate/VPN proxy. Treat a caller who can reach an enabled HTTP API as potentially untrusted with respect to other API sections and administration. Internal network placement does not imply administrator privilege.

The deployment operator, host configuration, signing key and protected local operator UI are trusted. This change does not defend against an attacker who already controls the host, reads the signing key, replaces the private control-plane store or controls the configured trusted proxy. It is not a multi-tenant isolation project.

API scopes restrict entry points. They do not by themselves sandbox everything an authorized workflow/agent can do through its configured tools. Preserve existing tool/capability/approval boundaries and do not give ordinary runs a bootstrap-admin credential. Explain powerful execute/configuration permissions in the picker. Do not claim per-user privacy for every object where the application currently shares data; existing domain checks remain the boundary.

## 3.2 One authentication stack, distinct credential kinds

Retain JWT Bearer as the HTTP authentication mechanism. Do not add a cookie fallback for API endpoints or a second web identity framework merely to support local accounts.

Model the following server-recognized credential kinds:

| Kind | Issuance and verification | Administrative authority |
|---|---|---|
| Legacy machine JWT | Existing unmarked JWT validation path. | None by default, regardless of role/subject naming. |
| Registered machine JWT | Existing v1 issuer and token registry; current revocation semantics. | No new access-admin authority from `api`/`api.tokens.issue` alone. |
| API user session | Password login; new versioned managed token plus user ID/revision binding. | Ordinary account scopes only. |
| Configured admin session | Password login against the configured administrator hash; bound to current configured credential. | Access administration, when its HTTP exposure gate is open. |
| Trusted local operator | Existing server-side interactive capability; no bearer is synthesized for HTTP. | Direct local administration through its explicit adapter. |

A practical wire choice is to retain `cda_token_version=1` for machine issuance and add version `2` for the two session kinds. Keep the existing JWT `typ` convention unless tests justify a change. Explicit version/kind dispatch and server-side registration provide the separation; an arbitrary type claim is not authoritative. Unknown combinations fail closed. JWT best practice requires separating token validation contexts rather than trusting claims from another token class. [S01, S05]

Extend registry metadata additively with a credential kind and optional account binding. Old documents must still deserialize and validate as machine records. New session metadata is never returned as a raw persistence object. Avoid changing every machine token to v2, changing the signing key, or relabeling existing subjects as accounts.

For user sessions the persisted metadata and signed claims must agree on ID, subject, kind, granted scopes, account ID and authentication revision. Also validate required expiry/issuer/audience/signature/algorithm and registration status. Reject duplicate/conflicting security-critical claims and malformed variants. Restrict accepted algorithms explicitly to the configured supported algorithm; do not let a token choose a different verification scheme.

## 3.3 Minimal account model

A proposed persisted `ApiUserRecord` has:

- immutable `Id` (GUID) and normalized unique username;
- display username and display name;
- `Enabled`;
- salted versioned `PasswordHash`;
- canonical `AllowedScopes` for business API capabilities;
- monotonic `AuthenticationRevision` for invalidating old sessions;
- `CreatedAtUtc`, `UpdatedAtUtc` and a concurrency/document revision.

Do not add PartyId, organization ownership, tenant membership, email confirmation, billing, a role editor or a foreign key into the currently selected application database. A name change never changes the stable user ID or lets a new account inherit the deleted account's sessions.

For a predictable first version, normalize usernames with a documented invariant/case-insensitive rule and restrict login names to a small unambiguous character set, for example ASCII letters/digits plus `.`, `_`, `-`, `@`, with a bounded length. Display names can be Unicode. Reserve the configured administrator username after the same normalization. Passwords are not trimmed or case-normalized. A proposed initial password policy is 12–256 characters, allowing passphrases and Unicode, with no silent truncation; use the same rule in creation, reset and the local hash helper. Reject control characters in display/log-facing fields; never construct filesystem paths from usernames.

A single configured administrative identity is sufficient. Ordinary persisted API users are non-admin. Their create/update DTOs do not include `IsAdmin`, arbitrary roles, caller identity, `AuthenticationRevision`, password hashes or token-kind fields. Creation and password reset take a write-only password. A generic user edit never accidentally replaces a password with a blank value.

## 3.4 Persistence without a new database subsystem

Use the existing control-plane root and `DurableFileWriter` security/durability conventions. A small `FileApiUserStore` with one schema-versioned private document is a reasonable default: one coordinated read/check/write operation makes username uniqueness and revision updates atomic. Per-user files are also acceptable if uniqueness/index consistency is solved without fragile partial writes.

The control-plane persistence contract/record belongs alongside current infrastructure control-plane contracts. ApiAccess application services can remain in the current `CanDoItAll.Modules.Workspace.ApiAccess` owner to avoid a broad relocation during merge preparation. Keep them free of Razor, `HttpContext` and endpoint routing. Web implements the caller-authority adapter and maps transport DTOs. Foundation must not reference Workspace.

Do not use a new SQLite database: current repository guidance specifically does not make SQLite a base runtime dependency. Do not put account authentication into `Workspace_Settings` or an active database profile. Switching profiles must not change who can log in or resurrect deleted credentials.

Missing/corrupt/unsupported credential records fail closed. A malformed store must not be silently overwritten by an empty one. Log safe operational diagnostics. Test cross-process/coordinated username creation, interrupted writes, restart, private file permissions on Linux and Windows behavior. A single active application process per control-plane root is the deployment target; distributed multi-host identity replication is not being promised.

## 3.5 Passwords and the configured administrator

Use `PasswordHasher<T>` from the framework, independently of the full Identity user-management stack. Microsoft recommends the high-level hasher rather than manually assembling a password store from low-level PBKDF2 primitives. Use a documented, benchmarked work factor and versioned hashes; OWASP currently gives separate iteration guidance for the selected PBKDF2 PRF. Do not equate the framework default with an automatically sufficient policy or invent a custom fast hash. [S02, S03]

A reasonable starting policy with the current V3 HMAC-SHA512 implementation is at least the reviewed OWASP work factor (220,000 iterations), verified against the actual package/runtime in this repository. If the runtime implementation differs, select the matching policy rather than copying the number blindly. Benchmark on representative deployment hardware and bound login concurrency. Store the salt and algorithm/work parameters in the hasher's standard format. Support success-with-rehash for persisted users without losing concurrent scope/password changes.

Provide a tiny local hash-generation command/tool that uses exactly the same hasher/options as login. Read passwords without terminal echo and not from a command-line argument. The tool may print the resulting hash for deliberate operator use but never the password. Do not hard-code demonstration hashes or example passwords in deployment defaults.

Proposed bootstrap settings are username and password hash. Secure login enabled without a valid configured administrator is a startup error, not a reason to create `admin/admin` or turn authentication off. Ordinary users can be provisioned locally before remote administration is exposed. The configured admin record is read-only in UI/HTTP account CRUD: replacement/reset is a deployment operation. Its current credential binding must be checked for every admin session, so replacing its hash and restarting invalidates old admin sessions without changing the JWT signing key or invalidating machine tokens.

A server-private fingerprint of the configured credential stored with the admin session, or a persisted credential-generation value maintained when configuration changes, can implement this binding. Never expose the hash/fingerprint in JWT payloads or DTOs. Do not require the operator to remember to increment a separate version manually for security to work.

The default development convenience is **disabled new login/administration**, retaining the existing open business API mode, or a real developer-configured hash for testing the secure mode. No mock admin handler is required. If an existing test-only fake is useful, it stays in test composition, never in the shipped production fallback path.

## 3.6 Login, re-login and session revocation

`POST /api/access/login` accepts only username and password. The server resolves the principal kind and current permitted scopes and issues a registered session token. The client cannot request another subject, admin role, arbitrary scopes or an unlimited lifetime.

Use a separate configured user-session lifetime, proposed default 60 minutes, without changing existing machine-token lifetime defaults. The UI stores the bearer in memory where practical and returns to login after expiry. Passwords are not retained in browser storage to implement automatic re-login. This directly solves “admin must send a new JWT” without creating a refresh-token store, rotation races or an OAuth server.

`GET /api/access/me` returns the authenticated session user's safe identity and current effective scopes. `POST /api/access/logout` revokes only the current registered session. Neither endpoint lets a user administer another user's tokens. Machines use the existing API contract, not the user session endpoints.

Allow zero business permissions without accidentally falling back to `api`. Because the current registry requires at least one scope, a concrete small option is a reserved `api.session` capability automatically added to new sessions. It only enables self-session endpoints and has no business or administrative wildcard effect. It is not user-selectable. Alternatively adapt the registry's session validation explicitly, while preserving its machine-token empty-scope rejection. Pick one approach and test it; do not weaken the existing regression test to accept broad defaults.

When a password is reset, a user is disabled/deleted, or business permissions change, atomically update the authentication revision or remove the account. Every subsequent user-session authentication checks the current record and revision. Re-enable does not restore a previous revision. A concurrent login/reset cannot produce a usable token with superseded rights. Already admitted work is not transactionally rolled back; existing cancellation/approval behavior still governs in-flight effects.

For long-lived event streams, a handshake check alone is insufficient. Check expiry and current authorization at a bounded heartbeat/event interval and close the stream on revocation. Do not keep sending buffered sensitive events after recognizing revocation. Document the bounded interval (the existing 15-second heartbeat is a natural starting point), and do not claim instantaneous cancellation of an already-running external effect. Do not introduce a perpetual generic polling job for every account.

Use generic invalid-login responses for unknown user, wrong password and disabled account, with comparable password-hash work for nonexistent accounts. Bound body/field sizes, concurrent expensive hashing, and failed attempts. Apply a bounded per-client/per-normalized-user throttle with safe retry responses, not a permanent account lockout trivially triggerable by another VPN user. Do not log credentials. Infrastructure failure is not a successful fallback login. [S02, S03, S04]

## 3.7 Authorization and the capability catalog

Extend the existing scope catalog with a small set of section capabilities. Examples for previously general-only families are `api.projects.read/write`, `api.agents.read/write/execute`, `api.workflows.read/write/execute`, `api.processes.read/write/execute`, `api.prompts.read/write`, `api.crm-hr.read/write`, `api.plugins.read/write` and `api.settings.workspace.read/write`. Resolve actual route groups before finalizing names. Reuse existing precise scopes instead of renaming or duplicating them.

One catalog should describe canonical name, section, label, description, selectable credential kinds and whether an operation is sensitive. Both token and user pickers consume it. A server-side validation method is authoritative; UI selections are only input.

Ordinary users receive explicit capabilities, never the backward-compatible broad `api` wildcard or reserved administration capabilities. “Select all” expands the currently available ordinary scopes; it is not a permanent grant to future API sections. Machine tokens may retain broad `api` where already supported; label that option clearly. Unknown scopes in newly submitted grants are rejected unless contributed by a registered, authoritative module catalog.

Avoid an over-restrictive parent policy: an exact `api.workflows.respond` token must not suddenly also need `api.workflows.read` just because all workflow paths share a group. The ordinary endpoint rule can be `api OR exact required data scope`; the existing privileged endpoint keeps its exact requirement. Keep domain requirements as additional checks. [S07] Permission classification is semantic, not merely `GET=read`: a probe or export can have effects/sensitive content.

The administrative HTTP policy requires both an enabled surface and a **server-validated configured-admin session**. A reserved `api.access.manage` scope can describe the capability, but possession of that claim on a machine token is not enough. The low-level machine issuer cannot mint a user/admin session kind. Any `api.tokens.issue` compatibility behavior must not create an indirect administrative escape.

Enforce administrative authorization in the owning application service via a narrow access interface implemented by the host. Direct UI uses trusted local authority; HTTP uses the verified admin session. No normal request becomes admin because the interactive provider is unavailable. Avoid duplicate ad hoc checks in every Razor callback.

## 3.8 Route completeness and cross-boundary calls

Inventory actual endpoint metadata from the composed host: API route groups, routes mapped directly on `app`, shared-provider aliases, project-structure/runtime paths, event streams, attachments, authorized downloads and OpenAPI. Classify intentionally anonymous status/health/discovery operations narrowly and document them. All other exposed business operations require the correct section decision in secure mode. A route-string prefix alone is not a reliable authorization mechanism.

Add a meaningful coverage test for endpoint metadata, plus representative real requests for every section and sensitive operation. This is a security invariant, not a brittle test that freezes the number of endpoints or partial classes. Endpoint coverage cannot be inferred from a static list that was never compared with the real host.

Keep current subject mapping and existing caller-bound processes/leases/history semantics. API accounts are stable principals, but per-resource ownership is not being redesigned. Do not solve a failing endpoint test by substituting a local operator or broadly disabling its resource policy. For internal HTTP calls, reuse the current controlled credential/caller mechanism; never pass an env-admin session to all agents or providers.

## 3.9 Browser, proxy and operator boundaries

Use same-origin deployment of the API-only UI where possible. If cross-origin development is needed, use ASP.NET Core CORS with exact configured origins and narrowly relevant methods/headers. Bearer authentication does not require a broad credentials-enabled cookie policy. Do not combine arbitrary origins with credentials. CORS is a browser behavior policy, not authorization for direct clients. [S06]

Require encrypted transport for real username/password and bearer traffic, typically HTTPS terminated at the known proxy; a loopback development exception can be explicit. Do not mistake `RequireHttpsMetadata=false` for an HTTP transport policy. Use trusted forwarded headers, not arbitrary client-supplied `X-Forwarded-Proto`, to determine original TLS where required. A private VPN/network can be part of the operator's transport model, but cleartext credentials must not be exposed on an uncontrolled hop.

A server profile must keep the backend port off untrusted interfaces and configure a default-deny proxy allowlist. Block SSR routes, `/settings`, `/_blazor` including negotiate/reconnect transports, `/_dev`, unapproved file routes, and any backend fallback. Allow additional API/download paths only after inventory and authorization review. Blocking `/settings` alone does not hide the interactive administrative UI. Do not use source IP appearing as localhost behind the proxy as evidence of an HTTP administrator.

The HTTP master switches and signing/bootstrap secrets are immutable deployment inputs in v1. Show their effective state in Settings; changing them requires configuration and restart. User records and token metadata remain editable through protected application services. This distinction prevents “administrative API enables itself” and avoids a generic remote configuration editor.

## 3.10 Explicit non-goals and compatibility changes

Non-goals: public registration, email, federation/OIDC server implementation, refresh tokens, multi-admin role hierarchy, multi-tenancy, new per-object ownership, global SSR login, distributed identity storage, automatic key rotation, public-internet certification, EGCP integration, and importing the alternative UI.

Intentional changes: remote token issuance becomes independently opt-in and admin-session-only; limited tokens no longer pass unrelated general API families; new account/scope writes are validated; bad secure configuration fails closed. Preserve existing machine credentials for legitimate permitted operations, existing `api` data semantics where supported, existing exact privileged checks, machine token TTLs and local operator UI administration.

For public or federated exposure later, prefer a standards-based identity provider and supported token flows rather than presenting this closed-instance password login as a general OAuth design. Microsoft explicitly recommends standards-based token acquisition for general production identity scenarios; the proposed bounded local mechanism is a product trade-off for this internal-instance requirement, not a claim of equivalent functionality. [S01]


---

<!-- Source: docs/04_API_AND_CONFIGURATION.md -->

# 4. Configuration, HTTP contracts and migration

**All new option/DTO names below are proposed.** Existing names are marked explicitly. Examples become usable only after implementation and supplying real private secrets through deployment configuration.

## 4.1 Configuration contract

| Configuration | Default / semantics |
|---|---|
| `Api:Enabled` | Existing main business API mapping switch. Existing runtime/project-structure exceptions remain documented. |
| `Api:Authorization:Enabled` | Existing secure JWT versus intentionally open business API mode. |
| `Api:Authorization:Issuer`, `Audience`, `SigningKey` | Existing signing/validation configuration. Do not change values during this migration unless deliberately rotating credentials. |
| `Api:Authorization:DefaultTokenLifetimeMinutes`, `MaxTokenLifetimeMinutes` | Existing machine-token limits; retain 480/1440 defaults. |
| `Api:UserAuthentication:Enabled` | New, default `false`. Exposes login and self-session operations. Requires the main API and JWT authorization enabled. |
| `Api:UserAuthentication:TokenLifetimeMinutes` | New, proposed `60`; positive and within the configured upper bound. Not a replacement for machine TTLs. |
| `Api:AccessManagement:Enabled` | New, default `false`. Exposes HTTP user/machine-token administration. Requires user authentication and JWT authorization enabled. |
| `Api:BootstrapAdmin:UserName` | New, proposed default `admin`; a username is not a default password. Reserved against ordinary account creation. |
| `Api:BootstrapAdmin:PasswordHash` | New, no default. Complete hash in the agreed standard hasher format; required when user authentication is enabled. |
| CORS/trusted-proxy options | Follow current host option ownership. Defaults are restrictive; do not invent a parallel set if an existing validated host option fits. |

Environment-variable mapping follows the normal double-underscore configuration convention, for example `Api__BootstrapAdmin__PasswordHash`. An env hash is not a substitute for the JWT signing secret: they protect different things and must not be derived from each other.

Use startup validation and a consistent options lifetime. Route exposure is decided at startup; do not hot-reload a secret/master switch while endpoint metadata still describes a different mode. A configuration change requires a controlled restart. Never infer a permissive mode from invalid options.

## 4.2 Validated modes

| Profile | Main API | JWT auth | User auth | Access management | Expected behavior |
|---|---:|---:|---:|---:|---|
| Trusted open desktop/dev | On | Off | Off | Off | Existing intentionally open business behavior. No HTTP login/user/token administration routes. |
| Legacy/service JWT instance | On | On | Off | Off | Existing machine JWT calls work; local trusted UI can manage tokens. No new user-login surfaces or HTTP token issuance. |
| Login users, local administration | On | On | On | Off | Users log in and use assigned sections. Admin HTTP CRUD/issuance stays 404 even with an admin session. Local trusted administration remains available. |
| API-managed server instance | On | On | On | On | Login works; only the configured admin session may administer access over HTTP. Ordinary users get 403. |
| Main API disabled | Off | Either | Off | Off | New surfaces and ordinary main API absent. Existing separately mapped surfaces follow their documented rules. |
| Invalid combination | Any | Off | On or management On | Any | Startup validation fails; do not expose an unauthenticated sensitive endpoint. |
| Invalid exposure combination | Off | Any | On or management On | Any | Startup validation fails, rather than silently claiming login/admin are exposed. |
| Invalid dependency | On | On | Off | On | Startup validation fails. |
| Missing/malformed required admin hash | On | On | On | Either | Startup validation fails without logging the hash. |

This intentionally tightens the old `/api/access/tokens` behavior: previously, an issue-scoped bearer could mint arbitrary tokens and the open-mode route returned 400. After implementation the administrative gate defaults closed, producing 404, and an enabled surface also requires the verified configured admin session. Document this compatibility change prominently. It does not require invalidating machine tokens used for data/provider calls.

## 4.3 Route contract

Keep the existing error envelope conventions; statuses below are normative for authorization, while success shapes should fit existing typed API conventions.

### Public discovery and session operations

| Endpoint | Exposure / authority | Result |
|---|---|---|
| `GET /api/access/status` | Existing main API gate; anonymous sanitized status. | Keep existing safe fields; add booleans indicating login/admin exposure without account names, lists or secrets. |
| `POST /api/access/login` | Main API + user-auth gates; anonymous credential submission with throttling. | 200 with a newly issued registered session JWT; generic 401 invalid credentials; 429 throttled; safe 503 for unavailable required infrastructure. |
| `GET /api/access/me` | User-auth gate; current valid user/admin session. | Safe account identity, kind/admin indicator and effective scopes. No password/hash/credential-generation data. |
| `POST /api/access/logout` | User-auth gate; current valid session. | Revoke current session and return 204 or a documented acknowledgement. Other sessions are not revoked. |

Do not require a bearer to reach login because it happens to be below the authenticated `/api` group. Use an explicit anonymous declaration for that route only. A valid machine token cannot call user-self operations as an arbitrary user. No `/register` or password-reset-by-email route is introduced.

A proposed login body is exactly `{ "userName": "reader", "password": "<entered password>" }`. A proposed response contains `token`, `tokenType`, `expiresAtUtc`, `userId`, `displayName`, `scopes` and a safe `isAdministrator` boolean. The administrator ID can use a reserved stable representation distinct from ordinary GUID IDs; finalize the contract consistently. Do not expose persistence classes directly.

Sensitive successful responses and login errors should carry appropriate `Cache-Control: no-store`. A 401 bearer challenge must preserve `WWW-Authenticate: Bearer` and must not redirect to a login page. A valid credential without the required permission receives 403, not a fresh login token.

### Access administration

All routes in this table require `Api:AccessManagement:Enabled` and the verified configured-admin session. When the gate is closed they are absent/404. When enabled, no/malformed credential returns 401 and an ordinary/machine credential returns 403.

| Endpoint | Operation |
|---|---|
| `GET /api/access/scopes` | Catalog for access administration, with sections and sensitive capability labels. |
| `GET /api/access/users` | Bounded/paged safe user list; optional search. |
| `GET /api/access/users/{id}` | Safe details and concurrency version. |
| `POST /api/access/users` | Create ordinary user from username/display name/password/enabled/scopes. |
| `PUT /api/access/users/{id}` | Update permitted profile/enabled/scope fields with concurrency protection. |
| `POST /api/access/users/{id}/reset-password` | Replace password and invalidate existing sessions. Password is write-only. |
| `DELETE /api/access/users/{id}` | Remove ordinary user and invalidate sessions; no identity reuse. |
| `GET /api/access/tokens` | Existing registry metadata search exposed safely; bounded pagination. |
| `POST /api/access/tokens` | Existing machine-token issuer, now protected by the new administrative boundary. |
| `POST /api/access/tokens/{id}/revoke` | Revoke selected registered token. |
| `DELETE /api/access/tokens/{id}` | Delete token registration; token remains rejected afterward. |

The token list should distinguish machine credentials from sessions, or default to machine credentials with an explicit session filter. Never make the machine-issuance request accept a session kind or user binding. Ordinary users do not receive this listing or an API for issuing arbitrary machine tokens.

Use a consistent optimistic concurrency convention, such as an ETag/`If-Match` or an explicit expected version, across update/reset/delete. Return the repository's established conflict/precondition status on stale input and leave the current record untouched. A duplicate normalized username returns a documented conflict. Missing ordinary user returns 404. Never allow a stale profile update to restore a disabled account's old permissions or password.

New DTOs should reject unexpected privilege-bearing fields, including `isAdmin`, role, kind, hash, arbitrary subject or revision. Do not silently accept an overpost and later rely on a UI client never sending it. Limit lengths, list sizes and allowed scope catalog entries. Return validation errors without reflecting passwords or secret configuration.

The configured bootstrap administrator can be described separately in the administrative status/UI, but is not a writable ordinary user resource. Attempts to mutate it through ordinary CRUD are rejected. Do not add a second reset path that lets a user change the deployment admin's hash.

### Business API additions

The eight colleague endpoints listed in `02_UI_BRANCH_DELTA.md` require the main API gate and their own business scopes in secure mode. They are not automatically available to every authenticated caller. Workspace-defaults access is not the same permission as account/JWT administration.

Preserve current domain authorization after section checks. An account authorized for an API section is not automatically authorized for every existing object-specific operation in that section. Keep response/download/event aliases consistent with their originating operation's authority.

## 4.4 Service interfaces and composition

Suggested boundaries, not mandatory class-count requirements:

- Account store: private persisted records and atomic mutations, independent of active database profile.
- Account administration service: safe DTOs, username/scope/password rules, concurrency, audit and authority checks.
- Session service: login, user/admin resolution, registration, self-session projection and logout.
- Credential validator: cryptographic/registry/account checks, producing a verified request credential descriptor.
- Administrative access interface: host adapter for a verified HTTP admin or existing trusted local interactive operator.
- Scope catalog/policy registration: one authoritative permission vocabulary and per-operation decisions.

Prefer reusing existing `IApiTokenService`, `IApiTokenRegistry`, `ApiTokenAdministrationService`, clock and durable-file facilities. Internal signing can be factored to avoid two diverging JWT implementations. Do not expose an unrestricted “mint arbitrary principal” method to ordinary request handlers or tools. Inject the narrow operations the caller needs.

## 4.5 Deployment and migration sequence

First deploy the compatible code with user authentication and HTTP administration disabled. Retain the current signing key/issuer/audience and persisted `api-tokens` directory. Verify a pre-existing machine token still accesses its intended shared-provider surface and that revoked registrations still fail.

Prepare the configured admin hash with the documented local tool and mount the private control-plane directory durably. Supply a random signing key only for new installations; generating a new key on every process start would invalidate existing credentials. Do not put hashes/keys in checked-in `appsettings` examples or print configuration objects at startup.

Enable user authentication with JWT authorization enabled. Manage initial users through the trusted local Settings UI, or explicitly enable HTTP access management and log in as the configured admin. Verify the proxy excludes the SSR/interactive transport and the direct container port is not reachable by ordinary clients. Use a regular scoped account for everyday API-only UI work rather than retaining an administrator token in normal navigation.

Record the administrative issuance compatibility change for any existing automation that used `api.tokens.issue`; the operator must explicitly adopt the new approved administrative path. Do not add a secret backward-compatibility bypass. Existing machine data access is distinct and stays supported.

Test restart with the same control-plane root and configuration. Account state, token revocation and grants persist. Rotate the admin password hash without rotating the signing key and verify old admin sessions fail while unrelated machine tokens still work. Switching the active workspace database must not change the account store.

For rollback, preserve a backup of the private store and document the supported schema direction. An older host may reject new session tokens, which is acceptable; it must not treat them as legacy unrestricted tokens. Do not delete or rewrite old machine records to make rollback appear clean.


---

<!-- Source: docs/05_SETTINGS_UI.md -->

# 5. Blazor Settings integration

## 5.1 Keep the existing interaction model

Extend the current `/settings` page and its `api-access` tab. Do not wrap the page/application/router in a new global authorization requirement, add a general login redirect, or replace direct server calls with HTTP calls to the same instance. The existing trusted local operator is an explicit administrative capability, not an instruction to make every anonymous HTTP caller an administrator. [D08, D09, D13]

Use the current shared CanDoItAll components. Repository guidance says the application no longer uses Radzen. Consult the Components MCP for existing contracts where needed; do not copy component library implementations or introduce a new form framework. [D16]

Keep feature state, mutation admission and authoritative read-back in the owning host/service. Renderers should not query persistence, invent an admin principal, manage JWT cryptography or write configuration. Follow the current UI-component-seams guidance without launching a separate unrelated extraction program.

## 5.2 Proposed content of API access

### Effective host configuration

Show business API state, JWT authorization, user login, HTTP access administration, safe issuer/audience/lifetime information and whether required credentials are configured. Show the configured admin as configuration-owned only on this trusted/administrative surface. Do not display its hash or the signing key.

Use explicit wording: “HTTP access administration is disabled. Trusted local administration is still available.” Distinguish this from “Current caller has no administration permission.” A disabled remote surface is not a broken local UI.

When the business API is intentionally open, prominently show: “API authorization is disabled. User permissions do not isolate anonymously accessible business operations.” Do not present account checkboxes as an active protection layer in that mode. New user/session controls can be unavailable with a clear configuration reason; do not activate a mock administrator automatically.

Master switches are read-only effective configuration in v1. Provide copyable **option names** and nonsecret instructions, not an HTTP setter for signing keys or self-exposure. Configuration changes require restart; do not show an optimistic toggle that did not change endpoint mapping.

### Machine tokens

Retain the existing `ApiTokenAdministrationPanel`, scope picker and metadata dialog. Improve shared scope validation/labels and distinguish broad compatibility grants from explicit sections. Preserve one-time token display, list/revoke/delete, expiry and pagination.

Label `api` as broad ordinary API access, not as “administrator.” Explain that `api.tokens.issue` on a legacy machine credential no longer independently grants remote access administration. If that reserved scope remains visible for compatibility, do not imply it can replace an administrative login.

### API users

Add a small `ApiUserAdministrationPanel` or equivalent module-owned feature. Reuse a list/detail layout with username/display name, enabled/disabled badge and an effective scope summary. The editor contains bounded username/display-name fields, an enabled switch and the same catalog-driven capability selector used by tokens, filtered to allowed ordinary-user grants.

New-user creation includes a password field. Editing an existing account does **not** load its password, hash or a placeholder that could be submitted as a new password. Password reset is a separate deliberate action with confirmation and an empty write-only password field. No self-registration link or email field is required.

Clearly state the consequence before save/reset/disable/delete: existing sessions for that user will stop working. Removing permissions is not deferred until token expiry. A reset must clear the entered password from component state after success and when cancelling/closing the editor.

Use explicit loading/busy/validation states and prevent duplicate submission while a mutation is in progress. Keep error messages safe. Re-read canonical safe account state after a successful write and keep dirty-edit/concurrency handling consistent with existing seams. A read-back failure after a committed mutation must not encourage a blind second create or claim the account was not created.

Do not allow the configured administrator to be deleted, renamed into an ordinary account, or reset using ordinary user controls. Show how the deployment operator changes it, without surfacing secrets. A normal account cannot turn itself into an administrator through form state or raw HTTP.

## 5.3 API-only UI contract

The alternative client only needs login, `me`, logout and the usual protected APIs for normal work. It can use `me.scopes` to avoid showing unusable sections, but server enforcement is authoritative. It does not need to fetch user inventories or token administration to initialize normal navigation.

For administrative screens it logs in as the configured admin and uses the independently enabled management routes. Same-origin proxy deployment avoids an unnecessary CORS burden. A cross-origin developer UI requires an explicit allowed origin, not an application-wide permissive response-header hack.

On 401, clear the expired/revoked bearer and show login; on 403, show insufficient permission rather than repeatedly asking for the same password. On a disabled administrative API's 404, use status discovery to show “not enabled” rather than guessing credentials are wrong. Do not save the password in local/session storage to simulate refresh tokens.

## 5.4 Required browser proof

Use the **shipped Settings page** at the repository's supported large-desktop viewport, not only a bUnit renderer or a new sandbox. Exercise local trusted administration with secure JWT mode enabled and HTTP access administration disabled, then independently exercise an enabled HTTP-admin scenario.

Create an account from Settings, select two concrete capabilities, log in through the real API, verify an allowed operation and a forbidden unrelated operation, edit/remove a capability, verify the old JWT fails, log in again and confirm the new grant set. Reset the password, check old-password rejection and new-password success; disable/re-enable and finally delete the account. Ensure re-enable did not revive an old token.

Also verify the existing machine-token issuance/list/revoke flow and ordinary `/settings` navigation did not acquire a global login redirect. Browser visibility is not enough: assert the persisted safe state and the corresponding real HTTP results after each mutation.

Use stable `data-testid` hooks for user controls, analogous to existing token hooks, without coupling tests to CSS layout or component internals. Evidence screenshots must not include entered passwords, hashes, complete tokens, vault contents or environment variables. Mask/omit the one-time token panel before screenshots rather than treating screenshots as a secret store.


---

<!-- Source: docs/06_VALIDATION.md -->

# 6. Validation and completion evidence

**Initial status of every application scenario below: NOT RUN.** These are acceptance criteria for Codex, not results from preparing this package.

## 6.1 Test selection without repeatedly running everything

The current repository's `docs/testing.md` explicitly separates the product build graph from Unit, Components, Integration, Memory and Playwright solutions. Its local loop requires changed production builds, a bounded owning filter, expected and actual discovery counts, and fresh test assemblies before `--no-build --no-restore`. Its static portability gate is mandatory for these edits. [D16, D17]

Use the current SharedInfo `candoitall-codeanalytics-mcp` skill. The canonical impacted-test capability is `code_analytics_impacted_tests_get`, a live-workspace query that does not require an architecture snapshot. [D18]

Request fields documented in that skill are `repositoryRootPath`, `testWorkspaces`, `changes`, `contextOnlyPaths`, `maxVisitedMembers`, `maxSelectors` and `maxReasonPaths`. Each actual change can include one-based inclusive `lineRanges` and `behaviorIntent`. Supply the separate relevant test solutions; the product `.slnx` does not magically include them.

For this task, authorization/DTO/mapping changes are `BehaviorChange` or `ContractOrShapeChange`; start conservatively with `Unknown` when unsure. Do not claim behavior-preserving intent merely because a method signature did not change. Read-only inspected files belong under `contextOnlyPaths`, not in the change set.

Validate workspace health and source-test discovery. Run all required selectors; evaluate conditional promotion triggers after expanding the diff, changing DI/serialization, discovering dynamic calls, or seeing an owning failure. `AllSuppliedSuites` is an instruction to run those supplied workspaces, not permission to skip because the selector list is empty. A broad impacted result can legitimately happen for authentication middleware. Record why a broader final run is necessary rather than repeating it after each small edit.

MCP output is test-selection evidence, not execution evidence. Use it to avoid irrelevant repetition, not to eliminate the explicit negative-security scenarios below. A static call graph may miss endpoint conventions, Razor lifecycle behavior, serialization and generated clients.

## 6.2 Concrete starting points

The reviewed `tests/Integration/CanDoItAll.Tests.Integration/ApiAccessAuthorizationIntegrationTests.cs` contains these relevant behaviors: [D12]

- `TOKEN_SCOPES_empty_selection_never_grants_broad_api_access`;
- `API_BOUNDARY_local_operator_ui_identity_does_not_authenticate_http_boundaries`;
- `Token_issuance_requires_explicit_privileged_scope`;
- `Authorization_disabled_token_endpoint_is_not_protected_by_the_scope_policy`;
- `Project_structure_routes_require_write_scope_and_bind_lease_owner_to_token_subject`;
- `TOKEN_LIFECYCLE_deleted_and_revoked_tokens_fail_real_http_requests`;
- `TOKEN_LIFECYCLE_corrupt_registration_fails_closed_on_http`.

The two existing issuance tests encode behavior intentionally changed by this task. Replace/extend their assertions with the new gate/admin truth table and record why; do not delete negative coverage to get green results.

Additional discovery seeds include `ApiTokenRegistryTests`, `ApiTokenAdministrationTests`, `ApiTestHost`, current shared-provider integration tests, current principal/history/authorized-file tests and Settings/Playwright tests. Some names were located by default-branch search; resolve the actual development symbols through CodeAnalytics before relying on them. Do not invent browser class names or expected discovery counts.

A bounded starting command, to be adjusted after the actual impact result, is:

```powershell
$tests = './tests/Solutions/CanDoItAll.Tests.Integration.slnx'
$filter = 'FullyQualifiedName~CanDoItAll.Tests.Integration.Api.ApiAccessAuthorizationIntegrationTests'

dotnet test $tests --configuration Release --list-tests --filter $filter /m:1
# Confirm the expected non-zero discovery count and that the assembly is current.
dotnet test $tests --configuration Release --no-build --no-restore --filter $filter /m:1
```

Build every changed production project directly and refresh the owning test graph first. Determine additional Unit/Components/Playwright filters from actual edits. Run `portability-static` exactly as currently documented, review genuine findings and intentional baseline changes, then finish with enforcement without baseline writing. No source-shape or class-count test should substitute for behavioral proof.

## 6.3 Proof layers

**Unit:** deterministic grant validation, user normalization, hash verification/error paths, account/token serialization compatibility, revisions, configuration and admin-policy decisions.

**Focused HTTP integration:** real JWT Bearer handler, registration store and endpoint policies using the existing API harness. The documented test-only InMemory database can support fast cases; do not replace authentication with a fake “always admin” scheme for these proofs.

**Production composition:** start the actual host with isolated control-plane files and supported database configuration. Use real HTTP requests over a bound loopback port, including the production middleware/error pipeline. This catches route mapping/configuration differences from the test harness. Final persisted business-endpoint/UI proof uses the supported production database; a blocked PostgreSQL/container lane remains blocked, not “passed via InMemory.”

**Browser:** Playwright against the shipped Settings page, performing actual account mutations and verifying subsequent API behavior. Isolated local/synthetic provider fixtures are enough; no real paid model requests are required for access-control proof.

**Deployment boundary:** a representative configured reverse proxy or the actual deployment routing test, proving SSR/interactive/dev endpoints are hidden and forwarding cannot create admin authority. A unit test of an IP helper alone is not this proof.

## 6.4 Acceptance matrix

Use valid request bodies and existing fixture objects for forbidden operations, so a 400/404 from malformed input is not mistaken for successful authorization denial. For denied writes, assert the account store, registry or relevant business state did not change. Disable automatic HTTP redirects while checking API statuses.

### A — Configuration and exposure

| ID | Scenario | Required result |
|---|---|---|
| A01 | Existing trusted open mode; all new flags off. | Ordinary open behavior preserved; all new login/admin routes and old HTTP token issuance absent/404. |
| A02 | JWT on, user auth/admin off. | Pre-existing machine token works for allowed data; no user-login/admin exposure. |
| A03 | User auth on, HTTP management off. | Login/me/logout work; every management route is 404 even with a valid admin session. |
| A04 | Management on with secure prerequisites. | Admin succeeds, no bearer is 401, ordinary user/machine is 403. |
| A05 | Main API off and new flags off. | New surfaces absent; existing exceptions match the documented contract, not a false global kill-switch assertion. |
| A06 | Every invalid flag combination, missing key/hash, malformed hash, invalid lifetimes. | Explicit startup/configuration failure; no open fallback; no secrets logged. |
| A07 | Generated OpenAPI under management off/on. | Disabled routes not advertised as callable; unique operation IDs, correct bearer/login metadata and no secrets. |
| A08 | Production error pipeline: absent route, denied request and service failure. | Status retained, safe API content, no 200 HTML fallback or unwanted login redirect. |

### B — Login, identity and credentials

| ID | Scenario | Required result |
|---|---|---|
| B01 | Configured administrator authenticates with real hasher and handler. | Registered admin session; no arbitrary role creation path. |
| B02 | Admin-created ordinary user authenticates and calls `me`. | Stable user ID and exactly allowed effective scopes; machine credential is not treated as that user. |
| B03 | Wrong password, unknown name, disabled user. | Same safe invalid-credentials response; comparable hash work, no account inventory disclosure. |
| B04 | Null/oversize inputs; role/sub/scopes/hash/kind/revision overposting. | Bounded validation rejection, no privilege escalation or secret reflection. |
| B05 | Username casing/normalization and duplicate concurrent creation. | Documented case behavior; one canonical identity, no duplicate or admin-name collision. |
| B06 | Wrong issuer/audience/signature/algorithm, unsigned token, expiry/future validity, invalid/duplicate critical claims. | Authentication denied; registry/kind rules cannot be bypassed. Test through real handler. |
| B07 | A machine JWT has admin-looking subject/name/role and reserved admin scope. | Still not an administrator; cannot use management routes or user-self operations. |
| B08 | Throttling with rotating usernames and untrusted forwarding headers. | Bounded limiter state/work, safe 429/retry behavior; no permanent attacker-triggered account lockout. |
| B09 | Login/token response, DTO serialization, logs and OpenAPI. | No-store on sensitive responses; no password hash/key/raw secret leakage. |
| B10 | Token expires and the user logs in again. | New registered JWT acquired without administrator issuance; expired token remains invalid. No stored browser password. |

### C — Account changes and revocation

| ID | Scenario | Required result |
|---|---|---|
| C01 | Remove/change a user's scopes while its JWT is active. | Old JWT rejected on the next validation; new login reflects the new set. |
| C02 | Reset password. | Old password rejected, old user sessions invalidated, new password works; machine tokens unaffected. |
| C03 | Disable, then re-enable. | Disabled login/session fails; re-enable permits new login but does not resurrect old JWTs. |
| C04 | Delete and recreate the same username. | New immutable ID; all deleted user's sessions remain invalid. |
| C05 | Logout one of two sessions. | Current token rejected; other session works until separately revoked/expired. |
| C06 | Concurrent profile update/password reset/login. | No stale write restores hash/permissions/revision; no usable superseded grants. |
| C07 | Configured admin hash rotation and restart with unchanged signing key. | Old admin session fails; new password works; legitimate machine token remains valid. |
| C08 | Registry/user-store corruption, missing user and unsupported schema. | Fail closed, safe diagnostic; no empty-store overwrite or fallback administrator. |
| C09 | Controlled clock around expiry and existing clock skew. | Behavior matches the documented policy; tests do not rely on flaky multi-minute sleeps. |

### D — Permission enforcement and legacy compatibility

| ID | Scenario | Required result |
|---|---|---|
| D01 | For each API section, a narrowly scoped user makes an allowed operation. | Correct positive response with expected safe state/body. |
| D02 | For each section, that user calls representative unrelated read/write/execute operations. | 403 before handler effects; no cross-section access from a merely valid JWT. |
| D03 | User has no business grants. | No implicit `api`; only explicitly defined self-session capability, or the chosen documented empty-grant behavior. |
| D04 | Existing broad `api` machine token. | Ordinary compatibility access works where historically intended; no new admin or exact privileged bypass. |
| D05 | Existing exact-only memory/chat/project-structure/shared-provider token. | Correct existing capabilities remain usable without requiring unrelated parent scopes. |
| D06 | Existing response/history/recovery privileged scopes and domain ownership. | Exact privileges and resource checks remain enforced; ordinary broad data access does not bypass them. |
| D07 | Legacy unmarked valid JWT and registered v1 JWT. | Deliberate compatible data behavior; neither gets typed admin authority. |
| D08 | Registered machine revoke/delete/corrupt record. | Existing real-HTTP rejection maintained. Unknown new session versions never fall through to legacy acceptance. |
| D09 | Real shared-provider catalog/inference client path with permitted local fixture. | Correct authorized operation and existing publication checks; no regression to client-instance use. |
| D10 | Endpoint-data-source inventory including direct routes, aliases, runtime/project-structure, files and streams. | No unclassified protected data operation; anonymous exceptions are explicit and justified. |
| D11 | Active event stream after expiry, user reset/disable/scope edit or token revoke. | Stream closes within the documented bounded check interval; no further recognized-forbidden events. |
| D12 | Client-supplied agent/owner/role headers and internal service calls. | Existing caller binding holds; no ambient local/admin credential supplied to ordinary execution. |

### E — Colleague endpoint integration

| ID | Scenario | Required result |
|---|---|---|
| E01 | Four process-definition reads, real fixture keys and valid filters. | Correct catalog/editor/roles/steps shapes and grants; bad key vs infrastructure failure distinguished. |
| E02 | Workflow template listing. | Actual template metadata and display summary; no accidental execution. |
| E03 | Template draft creation. | Stored draft/component consistent, correct lifecycle and existing permissions, no external model call. |
| E04 | Missing template/provider, second-write failure, duplicate/concurrent request. | Documented safe errors/semantics; no orphan or overwrite and no approval downgrade. |
| E05 | Workspace settings GET/PUT and authoritative read-back. | Allowed user succeeds; read-only/other-section user cannot mutate; safe validation and actual persisted values. |
| E06 | The eight added routes under API/auth flag combinations. | Correct gates and scopes; settings cannot escape the parent group by being mapped on `app`. |
| E07 | Generated OpenAPI compared with actual success/error JSON for changed contracts. | Correct DTOs/acknowledgements and no duplicate operations or stale contract lists. |

### F — UI, persistence and deployment

| ID | Scenario | Required result |
|---|---|---|
| F01 | Trusted local Settings with secure JWT on and management HTTP off. | No global SSR login redirect; local token/user controls work with explicit local authority. |
| F02 | Browser creates/edits/resets/disables/re-enables/deletes a user. | Actual writes and HTTP consequences verified after every action, including stale JWT rejection. |
| F03 | Browser scope picker, validation, concurrency, repeated save and committed-read-back failure. | No hidden broad grant, duplicate mutation or stale overwrite; accurate success/error state. |
| F04 | Browser existing machine-token issue/list/revoke. | One-time secret handling and registry lifecycle preserved. |
| F05 | Application restart using same private root and selected database-profile change. | Users, permissions and revocations persist independently of workspace database selection. |
| F06 | Real private-store filesystem on Windows/Linux or named unavailable lane. | Correct persistence/locking behavior and no plaintext passwords; permissions follow existing durable writer conventions. |
| F07 | Direct anonymous HTTP, loopback HTTP, forwarded request and untrusted interactive connection. | No HTTP local-operator elevation; trusted circuit logic not widened. |
| F08 | Representative proxy allows intended API only. | SSR, Settings, interactive transport, developer endpoints and unapproved file routes blocked; direct backend port not an escape. |
| F09 | Allowed/disallowed browser origins and CORS preflight. | Only configured cross-origin flows work; direct-client authorization remains independent. |
| F10 | Final source scan and targeted/broader closure. | Mandatory portability enforcement passes; actual test discovery/results and any broader trigger are recorded. |

## 6.5 Practical end-to-end acceptance journey

Start a clean isolated server profile with secure JWT, user login and HTTP management enabled. Generate secrets locally using the real helper; do not check them into the fixture. Capture only nonsecret configuration in the report.

Log in as the configured administrator. Create a user with, for example, Simple Chats read and workflow read. Authenticate that user through the real endpoint, prove allowed reads, and reject a project write, a workflow mutation and every management operation. Use existing fixtures so denial is not a disguised missing-resource error.

From the real Settings page remove workflow access. Immediately attempt the old bearer again, log in again and verify only the retained capability. Reset the password, disable/re-enable, then delete/recreate the username, checking old and new tokens at every step. Keep an independent pre-existing machine shared-provider credential alive and show that these user changes never revoke it.

Restart the host, verify persistence and admin-hash rotation semantics, then repeat the management-disabled mode to prove local Settings still works while management HTTP is 404. Finish with actual proxy isolation and correct error responses. Use small fixtures and no paid remote model calls.

## 6.6 Evidence format and completion rule

For each test lane record source HEAD, environment, changed production builds, exact commands, expected/actual discovery counts, pass/fail/skip counts, duration, CodeAnalytics required/conditional reasoning, and the redacted evidence location. For live HTTP record scenario ID, method/path, credential **kind**, status, expected/actual effect and safe response assertions, not the bearer/password.

For UI record actions performed, persisted state checked and linked HTTP assertions. Screenshot only safe UI states. A static screenshot of a form is not proof that its handlers work. A runner that “passes” after returning early from a disabled fixture gate is not live proof.

Use the provided `examples/validation-evidence.template.json` as a starting shape. Its initial `not_run` values must not be changed without execution. Keep planned, passed, failed, blocked, skipped and not-run evidence separate.

The implementation is ready for review only when required gates and actual API/UI behaviors pass. A real unavailable dependency can leave a clearly named lane blocked; that is an honest partial implementation, not validated merge readiness. Do not perform the actual development-to-main merge as part of this task.


---

<!-- Source: docs/07_SOURCES_AND_LIMITS.md -->

# 7. Source index, provenance and review limits

## 7.1 Provenance

Prepared on 20 September 2026. Repository evidence was read through the connected GitHub capability, using explicit commit refs rather than assuming code-search results represented development. File searches use the default branch and were used for path discovery only where followed by a pinned read.

The comparison reported UI ahead 35 / behind 223 relative to development. The common-base comparison identifies colleague additions; direct head-to-head diffs also contain development hardening that must not be reversed.

- [Development snapshot](https://github.com/fyziktom/CanDoItAll/tree/b82ffc57283f5e4819d82322e1c5bf836dcd9536)
- [UI snapshot](https://github.com/fyziktom/CanDoItAll/tree/e101d5db1478ea329a572db79c0104b927d97f15)
- [Reviewed common-base comparison](https://github.com/fyziktom/CanDoItAll/compare/b82ffc57283f5e4819d82322e1c5bf836dcd9536...e101d5db1478ea329a572db79c0104b927d97f15)
- [UI latest commit and patches](https://github.com/fyziktom/CanDoItAll/commit/e101d5db1478ea329a572db79c0104b927d97f15)

## 7.2 Development evidence

**D01 — Options, status, machine issuer.** [src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccess.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccess.cs). Read; configuration, issuance and claims reviewed.

**D02 — JWT registration and policies.** [src/App/CanDoItAll.Web/Api/ApiServiceCollectionExtensions.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/App/CanDoItAll.Web/Api/ApiServiceCollectionExtensions.cs). Read; authentication and authorization composition reviewed.

**D03 — API/documentation mapping and token endpoint.** [src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs). Read; parent group, flags, anonymous status and issuance reviewed.

**D04 — Private file token registry.** [src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApiTokenRegistry.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApiTokenRegistry.cs). Read; durability, schema, empty-scope invariant and mutation locks reviewed.

**D05 — Token administration application service.** [src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiTokenAdministrationService.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiTokenAdministrationService.cs). Read together with WebApiTokenAdministrationAccess.

**D05 — Token administration authority adapter.** [src/App/CanDoItAll.Web/Api/WebApiTokenAdministrationAccess.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/App/CanDoItAll.Web/Api/WebApiTokenAdministrationAccess.cs). Read; local-operator versus bearer-scope authority reviewed.

**D06 — Scope claim matching.** [src/App/CanDoItAll.Web/Api/ApiAuthorizationPolicies.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/App/CanDoItAll.Web/Api/ApiAuthorizationPolicies.cs). Read; exact/broad matching and case semantics reviewed.

**D07 — Managed token validator.** [src/App/CanDoItAll.Web/Api/ApiManagedTokenValidation.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/App/CanDoItAll.Web/Api/ApiManagedTokenValidation.cs). Read; legacy branch, managed ID and fail-closed registry checks reviewed.

**D08 — Shipped Settings page.** [src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor). Read; current API access tab and panel integration reviewed.

**D09 — Token panel.** [src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ApiTokenAdministrationPanel.razor](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ApiTokenAdministrationPanel.razor). Read; scope picker, list access and one-time display reviewed.

**D10 — Host pipeline.** [src/App/CanDoItAll.Web/Program.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/App/CanDoItAll.Web/Program.cs). Targeted lines 70-165; original peer, forwarding, auth, error handling and route composition.

**D11 — Workflow API.** [src/App/CanDoItAll.Web/Api/WorkflowsApi.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/App/CanDoItAll.Web/Api/WorkflowsApi.cs). Targeted lines 1-250; routing, current declarations and authentication remarks.

**D12 — Existing API authorization regressions.** [tests/Integration/CanDoItAll.Tests.Integration/ApiAccessAuthorizationIntegrationTests.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/tests/Integration/CanDoItAll.Tests.Integration/ApiAccessAuthorizationIntegrationTests.cs). Targeted lines 1-220; actual assertions read, not executed.

**D13 — Local operator trust.** [src/App/CanDoItAll.Web/Infrastructure/LocalOperatorAuthenticationStateProvider.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/App/CanDoItAll.Web/Infrastructure/LocalOperatorAuthenticationStateProvider.cs). Read; explicit interactive trust and original/effective peer checks.

**D14 — Scope names.** [src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccessScopeNames.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccessScopeNames.cs). Read; exact currently defined scope strings.

**D14 — Scope catalog.** [src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiScopeCatalog.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiScopeCatalog.cs). Read; labels, parser and managed-token claim constants.

**D15 — Workspace defaults model and service.** [src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs). Read; safe defaults fields and current database/activity operations.

**D16 — Agent entry point.** [AGENTS.md](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/AGENTS.md). Read; engineering, static gate and UI seams requirements.

**D16 — Engineering rules.** [.github/copilot-instructions.md](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/.github/copilot-instructions.md). Read; owner/dependency rules, English comments, shared components, no Radzen.

**D17 — Testing policy.** [docs/testing.md](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/docs/testing.md). Targeted lines 1-200; test workspaces, discovery counts and broad-gate triggers.

**D18 — CodeAnalytics execution guidance.** [SharedInfo skill](https://github.com/fyziktom/CanDoItAll.SharedInfo/blob/776a6a329dce5764ba07f6203a8d2ed103d39c2b/codex/skills/candoitall-codeanalytics-mcp/SKILL.md). Read the complete skill, including exact impacted-test capability, request fields and selector semantics. The MCP itself was not executed against a local CanDoItAll workspace during this preparation.

## 7.3 UI-branch evidence

**U01 — Relevant changed-file comparison and latest commit patches.** [UI commit](https://github.com/fyziktom/CanDoItAll/commit/e101d5db1478ea329a572db79c0104b927d97f15) and [comparison](https://github.com/fyziktom/CanDoItAll/compare/b82ffc57283f5e4819d82322e1c5bf836dcd9536...e101d5db1478ea329a572db79c0104b927d97f15). Inspected API/Program/ProjectStructure patches; not a review of every alternative UI component.

**U02 — Access route metadata.** [src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs](https://github.com/fyziktom/CanDoItAll/blob/e101d5db1478ea329a572db79c0104b927d97f15/src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs). Read; no new login/account implementation in this delta.

**U03 — Process definitions.** [src/App/CanDoItAll.Web/Api/ProcessDefinitionsApi.cs](https://github.com/fyziktom/CanDoItAll/blob/e101d5db1478ea329a572db79c0104b927d97f15/src/App/CanDoItAll.Web/Api/ProcessDefinitionsApi.cs). Read; all four added read handlers.

**U04 — Workflow templates.** [src/App/CanDoItAll.Web/Api/WorkflowsApi.cs](https://github.com/fyziktom/CanDoItAll/blob/e101d5db1478ea329a572db79c0104b927d97f15/src/App/CanDoItAll.Web/Api/WorkflowsApi.cs). Targeted first 360 lines plus relevant commit patches/helpers; template creation sequence inspected.

**U05 — Workspace defaults mapping.** [src/App/CanDoItAll.Web/Program.cs](https://github.com/fyziktom/CanDoItAll/blob/e101d5db1478ea329a572db79c0104b927d97f15/src/App/CanDoItAll.Web/Program.cs). Targeted host setup and lines 880-930; unguarded direct app mappings confirmed.

## 7.4 Primary external references

External references were checked during preparation. They support framework/security mechanics; the proposed product boundaries and default values remain engineering decisions for this task, not requirements attributed wholesale to Microsoft or OWASP. No external source files or long quotations are bundled.

**S01 — [Microsoft: Configure JWT bearer authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0).** Validation, challenge behavior, and the distinction between this bounded local design and general standards-based token acquisition.

**S02 — [Microsoft: Hash passwords in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/password-hashing?view=aspnetcore-10.0).** Use PasswordHasher rather than designing password storage directly with low-level primitives.

**S02 — [Microsoft: PasswordHasherOptions.IterationCount](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.passwordhasheroptions.iterationcount?view=aspnetcore-10.0).** Configurable work factor; check the package implementation rather than blindly accepting defaults.

**S03 — [OWASP: Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html).** Salted slow hashes, PRF-specific work factors and work-factor upgrading. Retrieved guidance gives PBKDF2-HMAC-SHA512 220,000 iterations; recheck at implementation time.

**S04 — [Microsoft: Proxy and load balancer configuration](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).** Trusted forwarders, original request information and middleware ordering.

**S05 — [IETF RFC 8725: JWT Best Current Practices](https://www.rfc-editor.org/rfc/rfc8725.html).** Algorithm validation, audience, cross-JWT confusion, explicit token context and mutually exclusive validation rules.

**S06 — [Microsoft: CORS in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-10.0).** Exact allowed origins and browser request policy; not an authorization replacement.

**S07 — [Microsoft: Policy-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0).** Requirements/policies compose, so parent requirements must not unintentionally exclude precise child capabilities.

## 7.5 Limits and things Codex must still verify

This was targeted source analysis, not an exhaustive execution audit. It did not run the application, compile the solution, fetch every transitive dependency, exercise API/Playwright tests, prove every endpoint policy, or validate the deployed reverse proxy. Reading an existing test is not a passing test result.

The UI branch's eight substantive method/route additions and the observed mapping gaps are directly grounded in the inspected files and patches. Statements about complete authorization coverage are requirements for the implementation inventory, not a claim that every current endpoint was read. Runtime/download/shared-provider aliases, process projection ownership at the actual future HEAD, existing hasher packages, current browser test classes and all current test discovery counts must be resolved in the checkout.

The package deliberately does not define EGCP, assume a particular external identity protocol for it, or attempt its integration. It does not recommend importing the colleague's old UI/framework versions.

The Git helper included here is optional and read-only with respect to the checkout. It was validated separately using a synthetic temporary Git repository; that does not provide CanDoItAll application evidence. All application scenarios in the evidence template remain not_run.

## 7.6 Decisions the implementer may refine

Final internal type placement, DTO names, exact section vocabulary, the shared signing helper, concurrency response convention and the transaction/compensation mechanism for workflow drafts can follow current owners and established contracts. Record material deviations in the implementation report.

Do not silently refine away the fixed invariants: independently disabled HTTP administration, verified admin authority, preserved local SSR operation, preserved legitimate machine tokens, actual section enforcement, account-change revocation, safe secrets, and real API/UI proof.


---

<!-- Source: examples/README.md -->

# Proposed examples

These JSON fragments describe the proposed option names. They are not active application configuration and are not proof the current host supports the new settings. Merge them deliberately after implementation.

Secure profiles intentionally omit the signing key and administrator password hash. Supply actual private values through the deployment configuration, for example the env keys `Api__Authorization__SigningKey` and `Api__BootstrapAdmin__PasswordHash`. Missing required secrets must cause validation failure, not a fallback. Preserve an existing installation's signing key to keep machine credentials valid.

Use the real application hash-generation helper implemented by Codex. Do not generate an unrelated hash format, pass passwords on the command line or check secrets into these examples. No example password/hash is provided.

`users-local-admin.proposed.json` enables user login while keeping HTTP administration closed. The trusted local Settings surface is independent of this HTTP gate. The examples omit deployment-specific proxy, TLS, database and control-plane mounts; they are not complete Docker deployment recipes.

`validation-evidence.template.json` is an unexecuted reporting template. Replace values only with actual observations.


---

<!-- Source: tools/README.md -->

# Optional read-only Git capture

`capture_review_snapshot.py` uses Python 3.10+ and an installed Git executable. It needs an existing local checkout that already contains both references. It does not access the network or change source, refs, the index, Git settings, or signing configuration.

Run from any directory, with an output path outside the checkout and its Git metadata:

```powershell
python ./tools/capture_review_snapshot.py --repo /path/to/CanDoItAll --out /path/to/review-evidence
```

The defaults are the pinned development and UI commits reviewed in this handoff. To inspect newer locally available references, use `--development development --ui-ref ui-refactoring-v2` and record the resulting commit IDs. This does not update those branches from a remote.

The output distinguishes the colleague's selected changes from their common ancestor from a head-to-head comparison. **Do not apply the head-to-head comparison:** it also reverses development-only changes. Only the eight selected API/composition source paths are captured. Reimplement useful changes against current development owners.

The destination must be new or empty. The helper checks that checkout HEAD and Git status did not change during its reads. Do not run it concurrently with a checkout or editing operation. These checks are not an atomic filesystem snapshot. Review captured patches before sharing: tracked source can contain sensitive information. The metadata includes the local repository path.

`helper-validation.json` records seven passing checks against a synthetic temporary Git repository during package preparation. They covered divergent commit counts, the two diff meanings, unchanged checkout state, unsafe output paths, output overwrite rejection and missing references. **This is helper validation only; no CanDoItAll build, API, persistence or browser test has run.**
