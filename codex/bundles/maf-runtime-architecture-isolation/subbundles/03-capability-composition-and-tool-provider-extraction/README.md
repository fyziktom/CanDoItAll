# 03-capability-composition-and-tool-provider-extraction

## Status

- `Ready`

## Objective

Extract capability access planning, capability composition, and registered runtime tool-provider composition from `MafAgentRuntime` into focused collaborators with direct tests and behavior parity.

## Covered Inputs

- M003, M004, M007, M009
- R004, R007, R010, R012

## Prerequisites

- SB01 responsibility map.
- SB02 runtime contracts and dependency classification.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Access.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Access.Policies.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.RuntimeToolProviders.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Access.RuntimeToolDescriptors.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ToolImplementationContractsTests.cs`

## Deliverables

- Extracted capability access planner/composer and runtime tool-provider composer.
- Direct unit tests for provider ordering, duplicate provider keys, duplicate tool names, metadata resolution, access filtering, approval wrapping, and diagnostics.
- Integration parity tests proving `MafAgentRuntime` still exposes the same effective tools for representative contexts.
- Reduced or justified reflection usage for moved capability/tool-provider behavior.

## Dependency Impact

- SB06 depends on this extraction for fake runtime tool-provider tests.
- SB07 depends on this extraction for performance measurements around provider enumeration, descriptor creation, filtering, and materialization.
- SB04/SB05 must preserve the same capability assembly contract when adding their drivers.

## Validation Depth

- `Critical behavior foundation`

## Implementation Steps

1. Move access planning and capability composition state into SB02 contract shapes.
2. Extract provider registration enumeration/sorting/validation into a composer.
3. Separate provider eligibility/prefiltering from expensive tool materialization where metadata permits.
4. Preserve approval wrapping, duplicate detection, metadata, context manifest sources, and diagnostics.
5. Add direct collaborator tests with fake runtime tool providers.
6. Keep integration parity tests through `MafAgentRuntime`.
7. Update proof and execution report.

## Scope Exceptions

- Do not extract provider client construction or finalizer behavior here.
- Do not optimize provider materialization beyond measured or structurally necessary changes.

## Do Not Do

- Do not weaken access filtering or approval wrapping.
- Do not make provider ordering nondeterministic.
- Do not hide provider failures.
- Do not leave production still using the old private composer after adding a new class.

## Acceptance Checklist

- [ ] Capability composition is directly testable.
- [ ] Provider composition is directly testable.
- [ ] Representative runtime integration tests still pass.
- [ ] Moved behavior no longer requires private reflection tests.
- [ ] Diagnostics and context manifest sources are preserved.

## Proof Required

- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`
- `## Production Behavior Artifact Matrix` for capability assembly/provider attachment records and diagnostics.
- Test transcripts for direct collaborator tests and integration parity tests.
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.

## Browser Validation Logging

- N/A unless UI-visible diagnostics are added.

## Progression Gate

- SB06/SB07 may rely on this phase only after provider composition can be tested without constructing the full runtime for moved behavior.

## Suggested Agent Prompt

```text
Implement SB03 only. Extract capability access/composition and runtime tool-provider composition into focused collaborators with direct tests and behavior parity. Preserve access filtering, approvals, metadata, diagnostics, and deterministic ordering.
```
