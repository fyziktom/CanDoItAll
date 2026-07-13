# SB05 Capability Composer Decomposition

## Status

- `Ready after SB04`

## Objective

Remove `RuntimeCapabilityComposer` as a partial-class cluster by extracting capability access planning, descriptor catalog mapping, attachment orchestration, context assembly, and composition metrics into cohesive owners.

## Success Criteria

- No final `partial class RuntimeCapabilityComposer` remains.
- Extracted owners are directly unit-tested.
- Adding a fake capability contributor does not require editing the old composer.

## Covered Inputs

- R07, R09, R10, R11.

## Prerequisites

- SB04 closure.
- Characterization tests for current capability access, descriptor, runtime tool provider, workspace tool, and compaction behavior.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.Policies.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.CatalogDescriptors.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.RuntimeToolDescriptors.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.RuntimeToolProviders.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentContextContributionTests.cs`

## Deliverables

- `RuntimeCapabilityAccessPlanner`.
- `RuntimeCapabilityDescriptorCatalog`.
- `RuntimeCapabilityAttachmentOrchestrator`.
- `RuntimeCapabilityContextAssembler` or equivalent if context assembly remains mixed.
- Explicit contributor interfaces only when multiple contributors or tests need them.
- Guard test blocking final `partial class RuntimeCapabilityComposer`.

## Dependency Impact

- SB06 depends on this seam so workspace tool families can contribute through a real catalog/attachment model instead of direct composer edits.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Characterize access plan and descriptor behavior.
2. Extract access planner and policy creation.
3. Extract descriptor catalog mapping for catalog/configured runtime capabilities.
4. Extract attachment orchestration and contributor boundaries.
5. Convert `RuntimeCapabilityComposer` to a thin coordinator or remove it if no longer needed.
6. Remove partial files or convert temporary bridges to top-level owners with a removal plan.
7. Add extension seam test.

## Scope Exceptions

- Do not deeply split `McpCapabilityBuilder` unless descriptor extraction makes it necessary.
- Do not split all capability builders in this subbundle unless they block composer removal.

## Do Not Do

- Do not replace one partial class with another.
- Do not create `CapabilityHelper`.
- Do not use service location to invoke contributors.

## C# Architecture Impact

Eliminates a known partial-class anti-pattern and creates extension-friendly capability boundaries.

## Boundary Ownership

Access planner owns policies. Descriptor catalog owns descriptor mapping. Attachment orchestrator owns ordered attachment. Builders own specific capability construction.

## Dependency Direction

Capability owners depend on abstractions/models and explicit implementation services. They must not depend on `MafAgentRuntime`.

## Pattern Decision

Catalog provider plus orchestrator. Builder only where construction validation exists.

## Testability Contract

Tests instantiate access planner, descriptor catalog, and attachment orchestrator directly.

## Partial Class Policy

Final state must not contain `partial class RuntimeCapabilityComposer`.

## Architecture Proof Required

- Source assertion for no final composer partials.
- Direct tests for access planner, descriptor catalog, and orchestrator.
- Extension seam test for fake capability contributor.
- CodeAnalytics member count comparison.

## Acceptance Checklist

- [ ] Composer partials removed or only temporary with explicit removal proof.
- [ ] Extracted owners have one reason to change.
- [ ] Existing runtime tool provider composition tests pass.
- [ ] Adding capability contribution avoids old monolith edits.

## Proof Required

- `proof/SB05/manifest.md`
- `proof/SB05/semantic-invariants.md`
- characterization/failing-first transcript.
- passing unit transcript.
- anti-stub/source assertion transcript.

## Browser Validation Logging

- N/A. Backend runtime composition only.

## Progression Gate

- SB06 may start only after workspace/capability contribution can happen through the new composer seam.

## Suggested Agent Prompt

```text
Execute SB05 only. Remove RuntimeCapabilityComposer as a final partial-class boundary by extracting access, descriptor, and attachment owners with direct tests and extension seam proof.
```
