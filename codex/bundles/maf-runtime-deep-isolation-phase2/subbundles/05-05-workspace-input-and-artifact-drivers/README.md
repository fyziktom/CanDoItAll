# SB05 Workspace Input And Artifact Drivers

## Status

- `Ready`

## Objective

Extract workspace plugin behavior, workspace search support, image analysis, artifact/document transformations, and input attachment preparation into named drivers/factories with direct tests and explicit dependencies.

## Covered Inputs

- N003, N005, N006, N007
- MAF2-R006, MAF2-R007, MAF2-R010

## Prerequisites

- SB04 closure proof for tool builder and MCP seams.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.WorkspaceRuntimePlugin.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.WorkspaceSearchSupport.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.StorageRuntimePlugin.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.InputAttachments.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceImageSetEvidenceBuilder.cs`

## Deliverables

- Top-level workspace tool factory/adapters for file, command, artifact, and image tools.
- Top-level workspace access policy helper.
- Top-level workspace search support.
- Top-level input attachment preparer and attachment result records.
- Direct tests using fake workspace file, command, artifact, provider runtime, and image services.

## Dependency Impact

- SB07 tests depend on direct workspace/input seams.
- SB08 performance closure depends on keeping heavy workspace/image dependencies lazy.

## Validation Depth

- Critical behavior phase.
- Requires semantic positive/negative proof for workspace access policy and attachment filtering.

## Implementation Steps

1. Separate pure path/access policy helpers from tool methods.
2. Move file/search/command/artifact/image behavior into named top-level drivers.
3. Move input attachment preparation out of the runtime partial.
4. Wire drivers into `ToolCapabilityBuilder` or workspace tool factory.
5. Add direct tests for allowed/denied paths, read/write policies, command argument flow, image model resolution, and request-scoped attachment filtering.

## Scope Exceptions

- Do not redesign workspace command execution APIs outside MAF unless required for compilation.

## Do Not Do

- Do not retain a single 900-line replacement plugin class.
- Do not silently broaden workspace write permissions.
- Do not make host command execution eager during runtime startup.

## Acceptance Checklist

- `WorkspaceRuntimePlugin` is gone or reduced to a thin top-level adapter.
- Workspace access decisions are testable without full runtime construction.
- Input attachment preparation is testable without full runtime construction.
- No new domain-specific tool behavior is added.

## Proof Required

- `proof/SB05/manifest.md`
- `proof/SB05/semantic-invariants.md`
- Build transcript.
- Focused workspace/input unit tests.
- Host-visible command smoke when command driver behavior changes.
- Source scan showing workspace/input drivers are not nested under `MafAgentRuntime`.

## Browser Validation Logging

- N/A unless workspace tools expose new browser-visible diagnostics.

## Progression Gate

- SB07 may not migrate runtime tests until workspace/input direct tests pass.

## Suggested Agent Prompt

```text
Implement SB05 only. Extract workspace, artifact, image, search, and input attachment behavior into named drivers with explicit dependencies and direct tests. Preserve access policy semantics.
```
