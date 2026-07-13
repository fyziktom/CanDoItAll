# C# Boundary Map

## Target Ownership

| Responsibility | Current owner | Target owner |
|---|---|---|
| Agent-facing filesystem methods | `WorkspaceRuntimePlugin` | New top-level `WorkspaceFilesystemRuntimePlugin` |
| Physical file/folder behavior | `WorkspaceFileService` | Unchanged |
| Path resolution and allowed-area enforcement | `WorkspacePathPolicy` plus MAF access profile checks | Unchanged, called before service operations |
| Runtime tool names and classifications | `ToolContractCatalog`, `ToolCapabilityRegistry`, templates | Extended with filesystem hash/archive tools |
| Broad runtime orchestration | `RuntimeCapabilityComposer`, `ToolCapabilityBuilder` | Thin wiring only |

## Dependency Direction

- `CanDoItAll.AgentFramework.Maf` may depend on `Core`, `Models`, and tool/capability abstractions.
- `Core` must not depend on MAF runtime classes.
- No new project is required in this phase because the extracted plugin is runtime-adapter behavior, not a reusable external SDK implementation.

## Why Not A New Project Yet

The filesystem behavior already lives in `CanDoItAll.AgentFramework.Core`. The missing part is the MAF adapter/tool exposure. Creating a new project for a thin runtime adapter would add reference churn without improving dependency direction. A future phase can move generic tool metadata into `CanDoItAll.AgentFramework.Tools` if the catalog becomes independent of MAF method delegates.

## Temporary Bridges

`ToolCapabilityBuilder` remains the capability-to-AI-tool composition point for this phase, but it delegates filesystem methods to `WorkspaceFilesystemRuntimePlugin`. The old filesystem method family is removed from `WorkspaceRuntimePlugin`.
