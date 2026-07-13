# SB02 Runtime Contracts And Configuration DTOs

## Status

- `Ready`

## Objective

Move private runtime-owned configuration DTOs, composition records, policy enums, and attachment/result records into top-level internal runtime contract/configuration files so later builders no longer depend on `MafAgentRuntime.*` nested types.

## Covered Inputs

- N002, N003, N006
- MAF2-R002, MAF2-R003, MAF2-R010

## Prerequisites

- SB01 closure proof.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.InputAttachments.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeExecutionContracts.cs`

## Deliverables

- Top-level internal configuration models for runtime, skill, MCP, RAG, AI context, memory, plugin, and built-in tool configuration.
- Top-level records for `RuntimeCompactionDecision`, `RuntimeCapabilityComposition`, attachment preparation results, and attachment analysis results.
- Parser/normalizer tests for configuration DTOs where behavior exists.
- No remaining composition/configuration records nested under `MafAgentRuntime` unless explicitly justified by SB01.

## Dependency Impact

- SB03 and SB04 depend on these DTOs.
- Builder extraction will remain dirty if builders must reference private runtime nested types.

## Validation Depth

- Critical foundation.
- Requires Semantic Adequacy Gate proof and production behavior artifact matrix for any new runtime state/record.

## Implementation Steps

1. Create a dedicated runtime configuration/contracts file or folder.
2. Move DTOs without changing JSON property names or behavior.
3. Replace references from `MafAgentRuntime.X` to top-level type names.
4. Add focused tests for any parsing/defaulting behavior.
5. Add source scan proving listed DTOs are no longer nested under `MafAgentRuntime`.

## Scope Exceptions

- Do not extract builders in this phase unless needed only to compile after DTO moves.

## Do Not Do

- Do not make DTOs public unless a real cross-assembly contract requires it.
- Do not hide config parsing behind `IServiceProvider`.
- Do not change capability catalog JSON semantics.

## Acceptance Checklist

- Runtime config DTOs compile as top-level internal types.
- `RuntimeCapabilityComposition` no longer requires nested type names for configuration state.
- Tests cover defaults/nulls for moved configuration types where behavior exists.

## Proof Required

- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`
- `dotnet build` transcript.
- Focused unit test transcript.
- Source scan for forbidden nested DTO names.

## Browser Validation Logging

- N/A: backend contracts/configuration refactor.

## Progression Gate

- SB03 may start only after `RuntimeCapabilityComposition` can reference top-level contracts and no builder extraction is blocked by private DTO types.

## Suggested Agent Prompt

```text
Implement SB02 only. Extract runtime configuration and composition DTOs from MafAgentRuntime into top-level internal types while preserving serialization and behavior. Add direct tests for moved behavior and record proof.
```
