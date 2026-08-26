# BR06 result

- Status: DONE
- Start HEAD: `79fa30b26b68de0462507fd9008662899eea3a85`
- End HEAD: BR06 checkpoint commit (`BR06: preserve provider schema and remove workspace residue`)
- Proof tier: Behavioral

## Implemented

- Confirmed that `ModuleAssemblies.All` discovers ProviderManagement entity configurations through `ProviderManagementModuleAssemblyMarker` while Workspace discovery remains independent.
- Preserved the six provider/shared-provider tables, primary keys, columns, lengths, indexes, concurrency tokens, foreign keys, delete behaviors, and nullable secret-reference semantics under their canonical ProviderManagement CLR ownership.
- Added model-metadata compatibility coverage for ProviderManagement marker discovery, all six historical physical table names, stable `Id` keys, provider field lengths, concurrency tokens, foreign keys, delete restrictions, and the non-relational nullable secret ID reference.
- Removed stale Workspace ownership terminology, test aliases, fixture names, and user-facing guidance. Capability and deletion tests now identify ProviderManagement administration as the owner.
- Kept Workspace dependencies only where the remaining behavior is genuinely workspace-scoped, including the opaque default-provider preference and connector plugin registry.
- Retained existing migrations unchanged. No metadata-only or functional migration was required.

## Boundary and compatibility evidence

- ProviderManagement marker discovery: PASS. All current provider/shared-provider entities and configurations are discovered from ProviderManagement; Workspace no longer owns their current CLR/configuration types.
- EF compatibility metadata: PASS. The physical tables remain `Workspace_ProviderProfiles`, `Workspace_ProviderSharePublications`, `Workspace_SharedProviderSources`, `Workspace_SharedProviderImports`, `Workspace_SharedProviderInvocations`, and `Workspace_SharedProviderServiceIdentity`; all use `Id` as the primary key.
- Migration diff: PASS. No migration or model-snapshot file changed, and no `CreateTable`, `DropTable`, `RenameTable`, or data-copy operation was introduced.
- Supported pending-model check: `dotnet ef migrations has-pending-model-changes` reported no changes since the last migration. The EF tools/runtime patch-version advisory was informational.
- Fresh CodeAnalytics snapshot `snap-20260826031422-a5726270` covers Infrastructure, PostgreSQL migrations, ProviderManagement, Workspace, and Composition with persistence, dependency, and risk analysis. It found the six current entities in ProviderManagement with their historical table mappings and no blocking persistence diagnostic or project-reference cycle.
- CodeAnalytics references to the former `CanDoItAll.Modules.Workspace` entity namespaces occur only in historical migrations/model snapshots and are retained migration history, not current production ownership.
- Production residue scan: PASS. Forbidden Workspace-provider ownership phrases, obsolete aliases, old registrations, and temporary BR02 compatibility types are absent.
- Remaining Workspace-related provider identifiers are classified and allowed: `IWorkspaceProviderCatalog`/`WorkspaceProviderOption` expose the narrow preference projection, `DefaultProviderProfileId` persists the opaque workspace preference, and `Workspace_*` table/migration names preserve database compatibility.

## Validation

- `dotnet build CanDoItAll.slnx --no-restore -nologo -v:minimal` — PASS, 0 warnings/errors.
- Unit, Components, Integration, ProviderManagement, Workspace, Infrastructure, PostgreSQL migrations, and host dependency graphs build — PASS, 0 warnings/errors.
- Exact frozen unit discovery — expected 46, actual 46.
- Exact frozen unit run — PASS; failed 0, passed 46, skipped 0.
- Exact frozen component discovery — expected 13, actual 13.
- Exact frozen component run — PASS; failed 0, passed 13, skipped 0.
- Exact frozen integration discovery — expected 18, actual 18.
- Exact frozen integration run — PASS; failed 0, passed 18, skipped 0.
- Component and integration runs used filesystem permission for the test harness's configured LocalAppData control-plane lock files.
- `git diff --check` — PASS; line-ending normalization notices only.

## Test-selection advisory

- The changed-file impacted-test analyzer was attempted against the Unit, Components, and Integration workspaces with a bounded 500-member traversal budget.
- It did not return within 90 seconds and was terminated; no analyzer-derived selectors or confidence are claimed.
- The frozen owning suites are the authoritative BR06 proof. Broad non-container validation remains the BR07 gate.
- Docker authorization is denied. The PostgreSQL lifecycle test in `SharedProviderPersistenceIntegrationTests` remains deferred to original SB07; the supported no-Docker EF check plus model, migration, materialization, transfer, secret-reference, and deletion metadata tests provide the available compatibility proof.

## Risks and remaining work

- Historical `Workspace_*` physical names and former Workspace CLR names in committed migration history are intentionally retained. Renaming them would create schema churn and violate the compatibility requirement.
- The Workspace connector plugin registry import used by runtime-projection integration setup is unrelated to provider ownership and remains required.
- Broad architecture guards and complete focused gates remain BR07 work.
