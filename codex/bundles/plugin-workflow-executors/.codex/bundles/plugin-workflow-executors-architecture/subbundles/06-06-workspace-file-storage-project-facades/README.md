# Workspace File Storage And Project Structure Facades

## Status

- `Completed`

## Objective

- Create plugin-safe workspace/storage/project-structure facades and fix concrete Workbench leakage.

## Success Criteria

- Plugin-safe facades exist for workspace files, storage access, and project structure operations.
- ProjectStructureWorkflowExecutor no longer depends on concrete Workbench service lookup through IServiceScopeFactory.
- Storage driver access is not exposed as a default plugin capability.
- Facade tests prove path/policy boundaries.

## Covered Inputs

- `R004`
- `R018`
- `R019`
- `R030`
- `F008`
- `F009`
- `F010`

## Prerequisites

- `SB01`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\Abstractions\StorageContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Files\WorkspaceFileContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Files\WorkspaceFileService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Paths\WorkspacePathResolutionContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workspace\WorkspaceScopeModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\ProjectStructureWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj`

## Deliverables

- IPluginWorkspaceFiles or equivalent scoped workspace file facade.
- IPluginStorageGateway policy wrapper or explicit storage-provider capability boundary.
- IProjectStructureRuntimeGateway/IPluginProjectStructureGateway abstraction.
- Refactored ProjectStructureWorkflowExecutor to depend on canonical gateway.
- Unit tests for path policy and project gateway behavior.

## Dependency Impact

- Later plugin module, workflow bridge, settings UI, and shop work depend on this foundation. Weak proof here causes duplication, secret leakage, or unstable plugin boundaries later.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Audit IWorkspaceFileService and current executor usage.
2. Define plugin workspace file facade methods with scope/context and operation limits.
3. Define storage gateway policy: normal plugins get scoped operations; storage-provider plugins require separate explicit capability.
4. Extract a project-structure runtime gateway interface with stable DTOs.
5. Implement the gateway using current Workbench/ProjectStructureAgentService without exposing the concrete service.
6. Refactor ProjectStructureWorkflowExecutor to inject the gateway directly or through optional capabilities.
7. Add tests for normal operation, missing gateway, path escape attempts, and storage capability denial.

## Scope Exceptions

- Do not implement full storage-provider plugin type in this subbundle.
- Do not rewrite project structure UI.

## Do Not Do

- Do not pass IServiceScopeFactory to plugins as a lookup path.
- Do not expose raw IStorageDriverRegistry to normal plugins.
- Do not allow absolute path escapes.

## Acceptance Checklist

- [x] Plugin-safe facades exist for workspace files, storage access, and project structure operations.
- [x] ProjectStructureWorkflowExecutor no longer depends on concrete Workbench service lookup through IServiceScopeFactory.
- [x] Storage driver access is not exposed as a default plugin capability.
- [x] Facade tests prove path/policy boundaries.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "WorkspaceFile|ProjectStructure|PluginCapability"`
- `dotnet build src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj`
- `dotnet build src\CanDoItAll.Modules.Workbench\CanDoItAll.Modules.Workbench.csproj`

## Browser Validation Logging

- N/A. SB06 changed runtime facades and DI boundaries, not project-structure UI.

## Progression Gate

- Passed. Project-structure workflow execution now targets `IProjectStructureRuntimeGateway`; Workbench is behind an adapter, workspace files are exposed through a constrained plugin facade, and normal plugin storage access uses `IPluginStorageGateway` rather than raw drivers.

## Suggested Agent Prompt

```text
Implement SB06 only.

Work outcome-first:
- Read this subbundle README, the root README, and reviews/01-execution-report.md.
- Verify prerequisites and exact source references before editing.
- Preserve the listed scope boundaries.
- Make the smallest correct change set.
- Capture required proof.
- Update reviews/01-execution-report.md.
- Stop if the progression gate cannot honestly pass.
```
