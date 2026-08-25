# BR03 result

- Status: DONE
- Start HEAD: `3045385c7`
- End HEAD: BR03 checkpoint commit (`BR03: relocate shared provider control plane`)
- Proof tier: Behavioral

## Implemented

- Relocated the complete shared-provider domain, persistence, reconciliation, relay, audit/recovery, and application implementation from Workspace to ProviderManagement without leaving forwarding copies.
- Moved shared-provider DI and hosted recovery-worker registration into ProviderManagement; Workspace DI no longer registers shared-provider services.
- Rewired AgentFramework, Composition, Web, tests, and the shared-provider E2E tool to consume ProviderManagement directly. Web now declares its direct ProviderManagement project reference explicitly.
- Preserved the existing shared-provider abstractions/HTTP protocol boundary and retained Web endpoint mapping in Web.
- Corrected the connector-manifest registration exposed by the focused integration run: the ProviderRegistry factory is registered with `AddScoped` so it can coexist with the typed shared-provider manifest source.
- Updated architecture/runtime characterization tests to assert the new owner and the actual runtime health/prompt gateway replacement registrations.

## Boundary and persistence evidence

- `Modules.Workspace/SharedProviders` absence check: PASS.
- Workspace DI shared-provider registration scan: PASS, zero matches.
- ProviderManagement forbidden Workspace namespace/reference scan: PASS, zero matches.
- Physical mappings remain `Workspace_ProviderSharePublications`, `Workspace_SharedProviderServiceIdentity`, `Workspace_SharedProviderSources`, `Workspace_SharedProviderInvocations`, and `Workspace_SharedProviderImports`, with their existing constraints and relationships unchanged.
- Migration diff scan: PASS, no migration files changed or generated.
- Post-change CodeAnalytics snapshot `snap-20260825235325-3a9e6dea` covers ProviderManagement, Workspace, AgentFramework, Composition, and Web with DI, persistence, and risk analysis enabled. ProviderManagement has zero project references and therefore no Workspace dependency; no project-level cycle exists. Reported internal AgentFramework module/type cycles are baseline findings outside this relocation.
- C# architecture gate: PASS. ProviderManagement owns the moved capability cohesively; outward consumers depend on the owner, no forwarding layer or partial-class split was introduced, and the boundary/characterization tests enforce the dependency direction.

## Validation

- `dotnet build CanDoItAll.slnx --no-restore --nologo -v:minimal` — PASS, 0 warnings/errors.
- Unit test project build — PASS, 0 warnings/errors.
- Integration test project build — PASS, 0 warnings/errors.
- Frozen unit discovery: 121 tests.
- Exact frozen unit run — PASS; expected 121, actual 121, failed 0, skipped 0.
- Frozen non-container integration discovery: 100 tests.
- Exact frozen integration run — PASS; expected 100, actual 100, failed 0, skipped 0. The final run used filesystem permission for the test harness's configured LocalAppData control-plane lock files.
- `git diff --check` — PASS; line-ending normalization notices only.

## Test-selection advisory

- Post-implementation impacted-test analysis inspected Unit, Integration, Components, and Playwright workspaces.
- It returned low confidence and an incomplete `AllSuppliedSuites` fallback for 7,480 source tests because dynamic/reflection dispatch prevents complete static relationship proof.
- It produced no healthy high-confidence additions. Broad non-container validation remains the BR07 gate.
- `SharedProviderPersistenceIntegrationTests` was not executed because its database fixture unconditionally provisions PostgreSQL through `docker compose`, while Docker is explicitly denied for this bundle. Mapping scans, migration-diff checks, state-model tests, and non-container behavior tests provide the available BR03 persistence proof.

## Risks and remaining work

- AgentFramework still owns the MAF-facing provider runtime gateway and shared-aware projection path; BR04 unifies runtime ownership and removes the ProviderManagement legacy gateway/adapters.
- UI/API/Composition and transfer dependencies are functional but still span transitional boundaries scheduled for BR05.
- The retained `Workspace_*` physical table names are intentional compatibility constraints, not logical ownership.
