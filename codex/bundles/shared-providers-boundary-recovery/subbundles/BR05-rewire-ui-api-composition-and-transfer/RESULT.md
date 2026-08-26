# BR05 result

- Status: DONE
- Start HEAD: `7df38d2d3fdc3e9071aac9a2c46a3fc5ff3f0c82`
- End HEAD: BR05 checkpoint commit (`BR05: rewire provider UI API and composition`)
- Proof tier: Behavioral

## Implemented

- Made `/agents?tab=providers` the sole provider editor. The AgentFramework provider panel and all provider selectors now use the canonical ProviderManagement runtime-administration port rather than Workspace provider methods.
- Moved the provider pricing editor into AgentFramework, removed Workspace's duplicate provider panel/editor state and DTOs, and retained a routed compatibility redirect from `/settings?tab=providers`.
- Reduced Workspace's provider knowledge to `IWorkspaceProviderCatalog` and `WorkspaceProviderOption`, which expose only opaque IDs, display names, and enabled state for the workspace default-provider preference.
- Rewired Web provider CRUD, diagnostics, model maintenance, and provider-event endpoints to `IProviderRuntimeAdministrationService` while preserving their route templates, wire models, HTTP mappings, and redaction behavior.
- Added the canonical ProviderManagement runtime-administration facade. It delegates persistence to the canonical registry, execution to the MAF diagnostics runtime, preserves source-managed availability/redaction rules, and persists personal-provider health/model-maintenance results.
- Removed ProviderManagement, SharedProviders, and AgentFramework model project references from Workspace. `AddWorkspaceModule()` now registers no provider/shared-provider services.
- Invoked `AddAgentFrameworkProviderManagement()` exactly once in Composition. Composition supplies the narrow Workspace catalog adapter and registers the workspace default-provider transfer after the canonical provider transfer handler.
- Split database-transfer ownership: ProviderManagement continues to own provider profiles, provider secrets/references, publications, imports, sources, and shared-provider state; Workspace transfers only its opaque default-provider ID. Missing target provider rows no longer suppress restoration of that preference.
- Preserved provider deletion semantics by clearing agent provider references inside the canonical ProviderManagement registry rather than relying on the removed Workspace facade.

## Boundary evidence

- Workspace source/reference scan: PASS. No ProviderManagement, SharedProviders, AgentFramework model, provider-registry, or provider-administration dependency remains; the only shared-provider text is an unrelated typed API scope constant.
- Workspace duplicate-editor scan: PASS. `ProviderManagementPanel` no longer exists, and `ProviderModelPricingEditor` exists only under AgentFramework.
- Provider endpoint and provider-panel characterization: PASS. Provider operations use `IProviderRuntimeAdministrationService`; retained `IAgentFrameworkWorkspaceService` usages concern agent/chat/capability state, not provider ownership.
- Registration scan: PASS. There is one invocation of `AddAgentFrameworkProviderManagement()` and one scoped registration of `IProviderRuntimeAdministrationService`.
- Workspace DI characterization: PASS. `AddWorkspaceModule()` contains no provider/shared-provider registrations; the remaining `IProjectManagementKnowledgeProvider` is unrelated project-management behavior.
- Transfer characterization: PASS. Canonical provider transfer remains in ProviderManagement, Workspace's handler reads/writes only `WorkspaceSettings.DefaultProviderProfileId`, and no transfer payload contains secret plaintext.
- Fresh CodeAnalytics snapshot `snap-20260826024232-bcc582d0` covers ProviderManagement, AgentFramework, Workspace, Composition, Web, and SharedProviders.Abstractions with DI and risk analysis. It reports no blocking errors or project-reference cycles. Its reported module/type cycles are internal AgentFramework baseline findings, not cross-project cycles introduced by BR05.
- CodeAnalytics finds exactly one scoped `IProviderRuntimeAdministrationService` registration, implemented by `ProviderRuntimeAdministrationService` in ProviderManagement.
- C# architecture gate: PASS. UI and Web depend on the canonical application facade, Workspace sees only a narrow opaque projection, and Composition alone joins the two module boundaries and transfer ordering.

## Validation

- `dotnet build CanDoItAll.slnx --no-restore -nologo -v:minimal` — PASS, 0 warnings/errors.
- ProviderManagement, Workspace, AgentFramework, Composition, Web, Unit, Components, and Integration project builds — PASS, 0 warnings/errors.
- Exact frozen unit discovery — expected 32, actual 32.
- Exact frozen unit run — PASS; failed 0, passed 32, skipped 0.
- Exact frozen component discovery — expected 14, actual 14.
- Exact frozen component run — PASS; failed 0, passed 14, skipped 0.
- Exact frozen integration discovery — expected 41, actual 41.
- Exact frozen integration run — PASS; failed 0, passed 41, skipped 0.
- Component and integration runs used filesystem permission for the test harness's configured LocalAppData control-plane lock files.
- `git diff --check` — PASS; line-ending normalization notice only.

## Test-selection advisory

- The changed-file impacted-test analyzer was attempted against Unit, Components, and Integration workspaces with a bounded 2,500-member traversal budget.
- It did not return within two minutes and was terminated; no analyzer-derived selectors or confidence are claimed.
- The frozen owning suites are the authoritative BR05 proof. Broad non-container validation remains the BR07 gate.
- Container-backed persistence validation remains unavailable because Docker authorization is explicitly denied for this bundle.

## Risks and remaining work

- `IProviderRuntimeAdministrationService` deliberately uses the stable MAF runtime models at the AgentFramework/ProviderManagement boundary so existing UI/API DTO behavior remains compatible; it does not reintroduce Workspace ownership.
- The narrow Workspace provider catalog is read-only by design. Provider creation, editing, deletion, health, pricing, source state, and secrets remain exclusively in ProviderManagement/AgentFramework.
- Persistence compatibility cleanup and residual-name removal remain BR06 work; broad guards and complete focused gates remain BR07.
