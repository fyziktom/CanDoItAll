# C# Dependency Direction

## Current References

From CodeAnalytics snapshot `snap-20260706235051-789dd62f`:

- `CanDoItAll.AgentFramework.Maf` references:
  - `CanDoItAll.AgentFramework.Tooling`
  - `CanDoItAll.AgentFramework.Tools`
  - `CanDoItAll.AgentFramework.Tools.Abstractions`
- `CanDoItAll.AgentFramework.Core` owns `IWorkspaceFileService` and filesystem behavior.

## Target References

- No new project reference is planned.
- New MAF runtime adapter type depends on existing `IWorkspaceFileService` and model records.
- Templates and tests reference new tool keys only through existing catalog structures.

## Forbidden References

- `Core` must not reference `Maf`.
- `Tools.Abstractions` must not reference runtime implementations.
- New filesystem plugin must not inject `IServiceProvider`.

## Cycle Risk

Low. The implementation does not move contracts across projects or add references.

## Proof Required

- Affected project builds.
- CodeAnalytics or source assertion that no new partial runtime class was added.
- Source assertion that filesystem methods moved out of `WorkspaceRuntimePlugin`.
