# SB06 Workspace Tool Family Extraction

## Status

- `Ready after SB05`

## Objective

Split `WorkspaceRuntimePlugin` into cohesive workspace tool families and shared access-policy/path services so file, command, script, artifact, and image behavior can be tested and extended independently.

## Success Criteria

- Workspace tool families have direct tests for metadata, policy, execution, and error behavior.
- Shared access policy/path service owns external alias normalization and denial cases.
- Host-visible command/script behavior has smoke proof when moved.

## Covered Inputs

- R08, R09, R10, R11.

## Prerequisites

- SB05 closure.
- Characterization tests for workspace access profiles, process intent filtering, protected delete, script side-effect manifest, and image-analysis model selection.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceSearchSupport.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceImageSetEvidenceBuilder.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/StorageRuntimePlugin.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/ToolCapabilityBuilder.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/ToolCapabilityBuilder.ConfiguredWorkspace.cs`

## Deliverables

- `WorkspaceToolAccessPolicyService`.
- `WorkspaceFileToolSet`.
- `WorkspaceCommandToolSet`.
- `WorkspaceScriptToolSet`.
- `WorkspaceArtifactToolSet`.
- `WorkspaceImageAnalysisToolSet`.
- Registration/catalog contribution through SB05 seam.
- Host-visible smoke for command/script tools if behavior changes.

## Dependency Impact

- SB07 dependency proof must include any new tool family registrations.
- SB08 final architecture proof depends on showing future workspace tools do not edit the monolithic plugin.

## Validation Depth

- Critical architecture phase with host-visible proof where command execution moves.

## Implementation Steps

1. Add/confirm characterization tests for current workspace plugin behavior.
2. Extract access policy/path service first.
3. Extract file/search/stat tools.
4. Extract command/dotnet/git tools with host-visible smoke.
5. Extract script tools and side-effect manifest handling.
6. Extract artifact/document/spreadsheet/image tool families.
7. Wire tool families through capability contribution seam.
8. Convert `WorkspaceRuntimePlugin` to a temporary delegating adapter or remove it.

## Scope Exceptions

- Do not introduce new document/PDF features.
- Do not solve MarkItDown availability.

## Do Not Do

- Do not loosen workspace access policy to make tests pass.
- Do not put all policy into string switches.
- Do not leave duplicate tool behavior in both plugin and tool sets.

## C# Architecture Impact

Replaces one large plugin type with cohesive tool-family owners and policy services.

## Boundary Ownership

Tool sets own tool execution. Access policy service owns permission/path rules. Provider gateway remains provider abstraction for image analysis.

## Dependency Direction

Tool sets depend on workspace abstractions and policy service. They must not depend on `MafAgentRuntime`.

## Pattern Decision

Catalog provider/tool-set modules with shared policy service.

## Testability Contract

Use fake file, command, artifact, and provider gateway dependencies. Include denial tests for mutation under read-only settings.

## Partial Class Policy

No partials allowed.

## Architecture Proof Required

- Direct tool-family tests.
- Source assertion that old plugin no longer owns each moved family.
- Extension seam test for adding a fake workspace tool family.
- Host-visible command proof where applicable.

## Acceptance Checklist

- [ ] Workspace access policy remains strict.
- [ ] Tool families are cohesive and directly tested.
- [ ] No duplicate production path remains.
- [ ] Runtime/capability composer no longer needs edits for a new workspace family.

## Proof Required

- `proof/SB06/manifest.md`
- `proof/SB06/semantic-invariants.md`
- unit test transcript.
- host-visible smoke transcript if command/script behavior moved.
- anti-stub/source assertion transcript.

## Browser Validation Logging

- N/A for browser. Host-visible command proof may be required and must be logged in `reviews/01-execution-report.md`.

## Progression Gate

- SB07 may start only after workspace tool extraction passes policy and host-visible proof.

## Suggested Agent Prompt

```text
Execute SB06 only. Split WorkspaceRuntimePlugin into cohesive tool families and access-policy services, preserve security behavior, and record host-visible proof for moved command/script behavior.
```
