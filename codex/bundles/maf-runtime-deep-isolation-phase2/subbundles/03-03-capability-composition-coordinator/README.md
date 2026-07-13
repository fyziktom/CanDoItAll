# SB03 Capability Composition Coordinator

## Status

- `Ready`

## Objective

Move capability composition orchestration out of `MafAgentRuntime` into a named coordinator that owns capability state creation, stage metrics, access-plan application, and builder wiring.

## Covered Inputs

- N002, N003, N005
- MAF2-R003, MAF2-R009, MAF2-R010

## Prerequisites

- SB01 closure proof.
- SB02 closure proof.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Access.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.RuntimeToolProviders.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeServiceCollectionExtensions.cs`

## Deliverables

- `IRuntimeCapabilityComposer` or equivalent test seam.
- `RuntimeCapabilityComposer` that performs capability state creation without being a `MafAgentRuntime` partial.
- Explicit dependency object for builder collaborators.
- `MafAgentRuntime` delegates capability state creation to the composer.
- Composition metrics remain stage-level and observable.

## Dependency Impact

- SB04 builders must plug into this coordinator.
- SB08 thin-runtime closure depends on moving this orchestration out of the runtime.

## Validation Depth

- Critical foundation.
- Requires Semantic Adequacy Gate proof and production behavior artifact matrix for composition metrics/state.

## Implementation Steps

1. Introduce coordinator request/result records that include agent, provider, model, session, context intent, and progress callback.
2. Move `CreateCapabilityStateCoreAsync` and `CreateCapabilityComposition` behavior into the coordinator.
3. Inject existing and future builders through constructor dependencies or a narrow dependency aggregate.
4. Keep `MafAgentRuntime` as the production caller.
5. Add direct tests for ordering, disabled capabilities, access filtering, and metrics recording.

## Scope Exceptions

- Detailed builder internals can remain in their current files until SB04, but the coordinator must not depend on `MafAgentRuntime owner`.

## Do Not Do

- Do not create a generic `MafRuntimeManager`.
- Do not pass `IServiceProvider` into every builder as a substitute for explicit dependencies.
- Do not remove access-policy checks while moving orchestration.

## Acceptance Checklist

- Capability state creation is unit-testable without constructing `MafAgentRuntime`.
- `MafAgentRuntime` no longer owns the main capability composition pipeline.
- Composition state references top-level contracts.

## Proof Required

- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`
- Build transcript.
- Direct composer unit tests.
- Boundary scan showing `CreateCapabilityStateCoreAsync`/composition orchestration moved out of runtime partials.

## Browser Validation Logging

- N/A: backend runtime composition refactor.

## Progression Gate

- SB04 may start only after builders can be injected into the coordinator without referencing nested runtime composition records.

## Suggested Agent Prompt

```text
Implement SB03 only. Extract capability composition orchestration into a named coordinator with explicit request/result types. Keep behavior parity and add direct tests for ordering, access, and metrics.
```
