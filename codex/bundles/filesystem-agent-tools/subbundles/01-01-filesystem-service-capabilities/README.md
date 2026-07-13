# 01-filesystem-service-capabilities

## Status

- `Complete`

## Objective

Extract the agent-facing filesystem operation family from `WorkspaceRuntimePlugin` into a cohesive top-level runtime plugin and expose missing service-backed operations.

## Covered Inputs

- Prepared filesystem commands for listing folders, copy folder, create folder, hash, zip, and unzip.
- Preserve allowed-area checks from file drivers and workspace policy.

## Prerequisites

- CodeAnalytics snapshot `snap-20260706235051-789dd62f`.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileService.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileQueryService.cs`

## Deliverables

- New `WorkspaceFilesystemRuntimePlugin`.
- Methods for list, search, read, stat, hash, create directory, write, append, copy, move, delete, zip, unzip, and diff.
- Explicit non-recursive listing method.

## Dependency Impact

- Unlocks runtime tool wiring in SB02.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Create the filesystem plugin.
2. Move MAF access checks for file read/write/manage into the plugin.
3. Add or reuse file-service operation results without bypassing the service.
4. Remove the filesystem method family from `WorkspaceRuntimePlugin`.

## Scope Exceptions

Do not move git, dotnet, script, document, spreadsheet, or image methods.

## Do Not Do

- Do not introduce a new partial class.
- Do not use raw `File`/`Directory` operations in the plugin except indirectly through `IWorkspaceFileService`.

## Acceptance Checklist

- [x] New plugin is top-level and cohesive.
- [x] `WorkspaceRuntimePlugin` no longer exposes filesystem methods.
- [x] Hash/zip/unzip are callable through the plugin.
- [x] Tests instantiate plugin directly.

## Proof Required

- Unit test transcript.
- Source assertion.

## Browser Validation Logging

- N/A.

## Progression Gate

- Passed. SB02 wiring completed after filesystem plugin compilation and direct plugin tests.

## Suggested Agent Prompt

```text
Implement SB01 only. Extract the filesystem runtime tool behavior without changing tool registration yet except where required for compilation.
```

## C# Architecture Impact

Responsibility extraction from a broad runtime plugin.

## Boundary Ownership

Filesystem runtime adapter becomes `WorkspaceFilesystemRuntimePlugin`.

## Dependency Direction

MAF depends on Core file service; Core does not depend on MAF.

## Pattern Decision

Adapter/facade plugin over existing service boundary.

## Testability Contract

Direct unit tests instantiate plugin with fake or real workspace file service.

## Partial Class Policy

No partial class allowed.

## Architecture Proof Required

Source assertion, direct tests, and no-new-partial check.
