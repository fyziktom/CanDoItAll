# BR02 result

- Status: DONE
- Start HEAD: `1c04e6da2`
- End HEAD: BR02 checkpoint commit (`BR02: extract canonical provider control plane`)
- Proof tier: Behavioral

## Implemented

- Moved the canonical persisted `ProviderProfile` entity and its EF configuration from Workspace to ProviderManagement while preserving the physical `Workspace_ProviderProfiles` table and its key, length, text, and concurrency mappings.
- Moved provider administration, editor/query contracts, validation, secret mutation policy, health/pricing administration, registry mapping, runtime snapshot projection, commit observation, and provider database transfer into ProviderManagement.
- Split provider database transfer from the opaque Workspace default-provider preference transfer. Provider transfer now copies provider profiles and referenced secrets without changing Workspace settings.
- Removed provider CRUD, entity/configuration, secret mutation, registry/projection, execution contracts, and provider transfer ownership from Workspace.
- Moved generic connector configuration contracts to SharedKernel and introduced neutral prompt-execution and health-check ports in AgentFramework.Core to avoid an inward dependency on Workspace.
- Rewired AgentFramework, Workbench, CrmHr, Composition, UI consumers, tests, and the shared-provider E2E tool to the new owner.
- Isolated the still-required direct provider adapters and runtime gateway as internal ProviderManagement compatibility code with explicit BR04 removal comments.
- Added `ProviderDatabaseTransferTests` to prove that provider profiles and referenced secrets transfer independently of the Workspace default-provider preference.

## Boundary and persistence evidence

- ProviderManagement project/source forbidden-Workspace scan: PASS, zero matches.
- Workspace canonical provider-ownership scan: PASS, zero matches.
- ProviderManagement project references contain only inner MAF, Foundation, Security, and shared-provider abstraction projects; no Workspace or outer feature reference exists.
- Workspace no longer contains the provider entity/configuration, provider administration service, canonical registry/mapper, provider secret mutation policy, provider execution contracts, or provider database transfer handler.
- `Workspace_ProviderProfiles` mapping is byte-for-byte equivalent in shape: same table, primary key, required/max-length mappings, TEXT payload, and concurrency token.
- Migration diff scan: PASS, no migration files changed or generated.
- Post-change CodeAnalytics snapshot `snap-20260825230234-0d503bad` confirms no ProviderManagement -> Workspace project edge. Existing internal AgentFramework/Workbench module/type cycles are unrelated baseline findings; no project-level cycle exists.
- C# architecture gate: PASS. Ownership is cohesive, compatibility code is internal and removal-tagged, no partial-class split or service locator was introduced, and the boundary test enforces the forbidden dependency.

## Validation

- `dotnet build CanDoItAll.slnx --no-restore --nologo -v:minimal` — PASS, 0 warnings/errors.
- Unit test project build — PASS, 0 warnings/errors.
- Integration test project build — PASS, 0 warnings/errors.
- Components test project build — PASS, 0 warnings/errors.
- Playwright test project build — PASS, 0 warnings/errors.
- Shared-provider E2E tool build — PASS, 0 warnings/errors.
- Frozen focused discovery: 52 tests total (`ProviderCatalogProjectionFailureTests` 12, `ProviderProfileSaveValidationTests` 30, `ProviderRuntimeProfileSnapshotServiceTests` 8, `ProviderManagementBoundaryTests` 1, `ProviderDatabaseTransferTests` 1).
- Exact focused run:
  `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-build --no-restore --nologo -v:minimal --filter "FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.ProviderCatalogProjectionFailureTests|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.ProviderProfileSaveValidationTests|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.ProviderRuntimeProfileSnapshotServiceTests|FullyQualifiedName~CanDoItAll.Tests.Unit.ProviderManagementBoundaryTests|FullyQualifiedName~CanDoItAll.Tests.Unit.ProviderDatabaseTransferTests"` — PASS; expected 52, actual 52, failed 0, skipped 0.
- `git diff --check` — PASS.

## Test-selection advisory

- Post-implementation impacted-test analysis inspected Unit, Integration, Components, and Playwright workspaces.
- It returned low confidence and an incomplete `AllSuppliedSuites` fallback for 7,480 source tests because dynamic/reflection dispatch prevents a complete static relationship proof.
- It produced no healthy high-confidence additions to the frozen BR02 set. The required broad non-container gate remains the named BR07 checkpoint.

## Risks and remaining work

- Workspace still consumes ProviderManagement types for shared-provider orchestration and the separate default-preference transfer check; BR03 moves the shared-provider control plane and removes that transitional consumption.
- AgentFramework still hosts the shared-aware snapshot loader and MAF execution adapter; BR03 and BR04 relocate shared orchestration and unify runtime execution.
- Internal legacy provider adapters intentionally remain until BR04 and must not become public contracts.
