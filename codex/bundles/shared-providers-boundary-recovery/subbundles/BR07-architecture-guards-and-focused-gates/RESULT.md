# BR07 result

- Status: DONE
- Start HEAD: `ba7b3aea3b98cce56e5e91fa11c79d8d3c1a9fe2`
- End HEAD: BR07 checkpoint commit (`BR07: enforce provider boundary and final gates`)
- Proof tier: Behavioral

## Implemented

- Added eleven explicit C# architecture guards covering ProviderManagement dependency direction, provider-specific AgentFramework source, Workspace ownership limits, Web shared-provider APIs, the agent provider editor, Workbench execution, legacy direct-inference declarations, EF configuration ownership, physical table names, production DI cardinality, and user-facing ownership terminology.
- Kept the Workspace exception narrow and typed: `WorkspaceProviderCatalog.cs` exposes only opaque provider options for the workspace default preference, while the preference transfer contains no provider entity, shared-provider, or secret behavior.
- Hardened the independent Python boundary gate for Windows repositories by using its generated-artifact-skipping walker instead of recursive `Path.rglob` calls that entered broken artifact links.
- Refined the Python gate to distinguish legitimate workspace preference/API-scope contracts from provider ownership, require ProviderManagement in the canonical solution, enforce one production registration, and reject ownership terminology in user-facing source.
- Updated the repository OS-branch allowlist with the reviewed server-only shared-provider source URI/socket policy.
- Removed stale work-package identifiers from active runtime-path fixture payload IDs and replaced a realistic-key-shaped test value with an explicitly synthetic non-key value.

## Guard evidence

- Exact architecture guard discovery — expected 23, actual 23.
- Exact architecture guard run — PASS; failed 0, passed 23, skipped 0.
- `check_provider_boundary.py --mode final` — PASS, exit 0, zero violations; final report written to `artifacts/provider-boundary-final.json`.
- ProviderManagement project/source guard — PASS. It is present in `CanDoItAll.slnx`, has no Workspace project reference, and its source has no Workspace namespace dependency.
- Workspace guard — PASS. There is no `SharedProviders` directory; the sole provider directory file is the narrow catalog contract; Workspace DI owns no provider/shared-provider registry, runtime, administration, or ProviderManagement registration.
- UI/API/Workbench guards — PASS. Provider UI uses ProviderManagement ports, shared-provider APIs are Workspace-free, and Workbench uses `IProviderPromptExecutionService` without the legacy execution request/response stack.
- Runtime guard — PASS. No production legacy direct-inference declaration remains under Modules, App, or MAF.
- Persistence/DI guards — PASS. All six EF configuration contracts and compatibility table names are found in ProviderManagement, none are configured from Workspace, the ProviderManagement marker is in module discovery, and production contains exactly one `AddAgentFrameworkProviderManagement()` invocation.
- Fresh CodeAnalytics snapshot `snap-20260826035805-39166230` covers ProviderManagement, AgentFramework, Workspace, Workbench, SharedProviders.Http, Composition, and Web with DI, persistence, dependency, and risk analysis. It reports no blocking errors or project-reference cycle.
- CodeAnalytics confirms ProviderManagement has no scoped Workspace dependency in the analyzed project graph and finds exactly one scoped `IProviderRuntimeAdministrationService` registration implemented by `ProviderRuntimeAdministrationService`.
- Reported module/type cycles are internal AgentFramework and Workbench baseline structure; no project cycle or cross-boundary ProviderManagement-to-Workspace dependency is present.

## Focused behavioral acceptance

- Frozen focused unit discovery — expected 168, actual 168.
- Frozen focused unit run — PASS; failed 0, passed 168, skipped 0.
- Frozen focused component discovery — expected 23, actual 23.
- Frozen focused component run — PASS; failed 0, passed 23, skipped 0.
- Frozen focused integration discovery — expected 122, actual 122.
- Frozen focused integration run — PASS; failed 0, passed 122, skipped 0.
- These suites cover personal provider create/update/delete, secret binding/replacement, deletion references, publication eligibility, catalog redaction, source CRUD/sync/reconciliation, imported-profile identity and disable behavior, personal/shared/hybrid materialization, effective revisions, fail-closed selection, relay authentication/authorization/rate/error/audit/recovery behavior, image routing, MAF execution, provider UI, Web APIs, DI, and transfer compatibility.
- Component and integration runs used filesystem permission for the test harness's user-scoped control-plane and test-platform files.

## Broad validation

- `dotnet build CanDoItAll.slnx --no-restore -nologo -v:minimal` — PASS, 0 warnings/errors. This includes the required ProviderManagement, AgentFramework, Workspace, Workbench, Composition, and Web dependency graphs.
- Unit and Integration project builds after the closing fixture/guard corrections — PASS, 0 warnings/errors.
- `dotnet ef migrations has-pending-model-changes` — PASS; no changes since the last migration. The EF tools/runtime patch-version advisory was informational.
- The prescribed one-time full non-container unit execution ran 6,889 tests: 6,886 passed and three repository-hygiene guards identified the stale fixture IDs, missing reviewed OS owner, and realistic-key-shaped fixture value.
- After the minimal corrections, exact reruns of those three failed guards passed 3/3. The two affected integration fixture surfaces passed 7/7. The full suite was not rerun because BR07 explicitly permits it only once.
- `git diff --check` — PASS.

## Deferred infrastructure

- Docker authorization remains denied for this bundle. The real PostgreSQL lifecycle coverage in `SharedProviderPersistenceIntegrationTests` and the separately authorized original SB07 lifecycle/E2E lanes remain deferred.
- No Docker, Podman, browser E2E, or original SB07 lifecycle command was run.

## Risks and remaining work

- The Python guard intentionally permits only `WorkspaceProviderCatalog.cs` as the narrow workspace preference projection and only `ApiAccessScopeNames.cs` for typed shared-provider authorization scopes. Any new Workspace provider ownership/runtime file still fails the guard.
- The reviewed `OperatingSystem.IsBrowser()` branches in `SharedProviderSourceUriPolicy.cs` protect server-only DNS/socket behavior and are now visible to the repository-wide OS-owner guard.
- BR08 must produce the exact handoff to original SB07, classify deferred commands, and close the recovery bundle without running those commands.
