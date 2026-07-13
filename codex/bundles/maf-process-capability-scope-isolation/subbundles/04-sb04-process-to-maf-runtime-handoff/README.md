# SB04 Process To MAF Runtime Handoff

## Status

- Status: `Completed`
- Criticality: `Critical integration foundation`
- Depends on: SB02, SB03

## Objective

Translate effective process-step capability scope into MAF execution metadata, runtime context intent, capability policies, required capabilities, and scoped prompt fragments.

## Covered Inputs

- Process-specific instructions must be added through process cooperation channels.
- Tool/skill/MCP suppression must happen during process steps.
- Forced tools/instruction carriers must be possible.
- REQ-MAF-003, REQ-MAF-004, REQ-MAF-005, REQ-MAF-008, REQ-MAF-009, REQ-MAF-010, REQ-MAF-011, REQ-MAF-012.
- NFR-003, NFR-005.

## Prerequisites

- SB02 MAF policy enforcement complete.
- SB03 process scope contract persisted on assignments.
- Read `bundle://architecture/01-target-solution.md`.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Metadata.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Runtime/AgentRuntimeContextAssemblyModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessStepBriefBuilder.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeLaunchVariables.cs`

| Source | Required attention |
| --- | --- |
| `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Metadata.cs` | Add scope metadata serialization. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs` | Apply/resolve typed scoped runtime policy metadata. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` | Add resolved scope to `CreateRuntimeContextIntent`. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Runtime/AgentRuntimeContextAssemblyModels.cs` | Carry scoped override into runtime options. |
| `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessStepBriefBuilder.cs` | Attach scoped instructions from validated scope only. |
| `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeLaunchVariables.cs` | Do not use launch variables as the durable primary scope channel. |

## Scope

- Add mapping from process-neutral scope targets to MAF capability selectors.
- Serialize mapped scope into execution metadata.
- Resolve mapped scope into `AgentRuntimeContextIntent`.
- Use mapped scope in MAF policy building.
- Attach scoped instruction fragments in process prompts only when compatible with the same scope policy.
- Add failure behavior for invalid metadata and unknown selectors.

## C# Architecture Impact

This is the integration phase. It must keep translation in the AgentFramework process adapter and not leak MAF concepts back into process core.

## Boundary Ownership

- `CanDoItAll.Modules.Processes` owns the process-to-AgentFramework mapping.
- MAF owns execution metadata parsing and runtime policy application.
- Process runtime owns durable assignment state only.

## Dependency Direction

The adapter may reference both process contracts and AgentFramework contracts. Process contracts must not reference MAF.

## Dependency Impact

- Expected impact spans `CanDoItAll.Modules.Processes`, AgentFramework Core metadata, AgentFramework Models, and MAF policy use.
- Downstream SB05 relies on this handoff to make development-specific capability ownership process-controllable.

## Pattern Decision

Use an adapter/translator with explicit validation results. Do not append prompt snippets in a separate path from policy compilation.

## Testability Contract

- Mapping tests for each process target kind.
- Metadata serialize/resolve tests.
- Invalid metadata tests that block governed execution.
- Prompt composition tests where a denied capability prevents matching scoped instruction attachment.
- Runtime context tests proving policies receive the scoped override.

## Validation Depth

- Unit tests are mandatory for translation and metadata resolution.
- Runtime integration tests are mandatory for context intent and prompt composition.
- Negative tests for invalid metadata are mandatory.

## Partial Class Policy

The existing `AgentFrameworkProcessExecutionAdapter` is partial-heavy. Do not expand it with large new policy logic. Prefer focused collaborators and keep partial edits limited to orchestration calls.

## Implementation Steps

1. Add process-to-MAF scope translator.
2. Update `BuildProcessExecutionMetadata`.
3. Add `ExecutionInvocationMetadata` apply/resolve methods.
4. Update `CreateRuntimeContextIntent`.
5. Wire scoped instructions in the process brief driver.
6. Add tests and proof.

## Do Not Do

- Do not serialize raw unvalidated policy JSON and parse it ad hoc in MAF.
- Do not let metadata parse failures fall back to unrestricted agent defaults.
- Do not attach prompt fragments for suppressed capabilities.
- Do not bypass existing allowed-operation filtering.

## Acceptance Checklist

- Process assignments drive MAF capability policy.
- Suppressed skills/tools/MCPs are absent from context.
- Required missing/denied capabilities block governed runs.
- Prompt fragments and policy are generated from one validated scope contract.
- Tests cover success and failure paths.

## Proof Required

- `proof/SB04/manifest.md`
- `proof/SB04/semantic-invariants.md`
- Production Behavior Artifact Matrix for metadata keys, context intent fields, translator output, and diagnostics.
- Test output.

## Browser Validation Logging

- N/A unless UI-visible process diagnostics are added.

## Progression Gate

- SB05 may start when process-to-MAF scope handoff is enforced and tested.

## Suggested Agent Prompt

```text
Execute SB04 only. Translate persisted process step scope into MAF execution metadata, runtime context intent, capability policy, required capabilities, and scoped instructions. Fail closed on invalid metadata and add tests.
```
