# Target Solution

## Repair Strategy

This bundle is not a product feature refactor. The target is a reliable validation baseline with small, ownership-aligned repairs:

- Repository hygiene tests remain active guards. Repair tracked artifacts/names or add narrow, reviewed exceptions only when an item is intentionally durable source.
- Runtime launch/watch tests should use current repository layout and realistic project references. Prefer correcting obsolete fixtures over changing production behavior when production is already right.
- Process-template tests should assert durable process semantics: validation ownership, repair routing, branch outcomes, and runtime/browser ownership boundaries. Avoid fragile exact prose unless the phrase is itself a shipped contract.
- Branch-signal recovery is production behavior. If completed process output declares a listed branch outcome, runtime dispatch must receive the corresponding `ProcessBranchSignalCodes.Outcome(...)` signal.
- EF migration checks must be proof-driven. A clean pending-model check means no migration; flaky runtime-switch behavior should be addressed through test isolation/global state reset.

## Boundaries

- `tests/Unit/CanDoItAll.Tests.Unit/*HygieneTests.cs`: repository guard ownership.
- `tests/Unit/CanDoItAll.Tests.Unit/ProjectStructureRuntimeLauncherTests.cs` and `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs`: launch plan ownership.
- `tests/Unit/CanDoItAll.Tests.Unit/WorkspaceRuntimeProcessToolsTests.cs` and `tools/App/CanDoItAll.Manager/WorkspaceRuntimeProcessTools.cs`: watch argument ownership.
- `Templates/Processes/processes/dotnet-feature-function-implementation/*` and `tests/Unit/CanDoItAll.Tests.Unit/ProcessDefinitionCatalogProjectionTests.cs`: process template invariant ownership.
- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs` and related process runtime tests: branch signal recovery ownership.
- `src/Foundation/CanDoItAll.Infrastructure/Persistence/*`, `src/Foundation/CanDoItAll.Migrations.PostgreSql/*`, and database runtime tests: migration/isolation ownership.

## Non-Architecture Decision

Do not introduce new projects, broad abstractions, or new test infrastructure unless a failure recurs in three or more places and a helper removes real duplication. The expected implementation is mostly test fixture repair, narrow production bug fixes, and validation scripts/transcripts.
