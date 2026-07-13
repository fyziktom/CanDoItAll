# SB04 Runtime Agent Factory Decomposition

## Status

- `Ready after SB01`

## Objective

Decompose `MafRuntimeAgentFactory` so runtime construction, handoff build, instrumentation, finalizer tool creation, script policy inspection, credential promotion, and chat-history setup have explicit owners.

## Success Criteria

- `MafRuntimeAgentFactory` is a narrow compatibility facade or removed in favor of focused owners.
- Build/policy/instrumentation owners have direct tests.
- `IServiceProvider` is not used as a general service locator in construction behavior.

## Covered Inputs

- R06, R09, R10, R11, R13.

## Prerequisites

- SB01 closure.
- Characterization tests for runtime build and handoff behavior.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeExecutionContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafModelParametersBuilder.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/MafAgentRuntimeHandoffTests.cs`

## Deliverables

- `MafRuntimeBuildCoordinator`.
- `MafHandoffRuntimeBuilder`.
- `MafHostedAgentFactory`.
- `MafToolPolicyInstrumentor`.
- `MafFinalizerToolFactory` if finalizer tool creation remains mixed.
- `MafScriptPolicyInspectionService`.
- Focused unit tests and DI registration changes.

## Dependency Impact

- SB03 depends on stable runtime build results.
- SB05 depends on capability composer dependencies being explicit after build decomposition.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Characterize normal and handoff runtime build.
2. Extract script policy inspection first if it has a clean file/path seam.
3. Extract tool instrumentation/finalizer tool creation.
4. Extract handoff builder.
5. Extract main build coordinator and hosted agent factory.
6. Replace factory internals with delegation and remove duplicate code.

## Scope Exceptions

- Provider SDK adapter internals stay in provider-specific classes unless build decomposition requires a narrow seam.

## Do Not Do

- Do not move construction behavior into DI lambdas.
- Do not inject `IServiceProvider` into every new owner.
- Do not create a factory that still owns all business behavior.

## C# Architecture Impact

Removes a major construction hotspot and makes runtime build decisions independently testable.

## Boundary Ownership

Build coordinator owns normal build. Handoff builder owns handoff. Instrumentor owns tool wrapping. Script inspection service owns policy reads.

## Dependency Direction

New owners depend on explicit services and records. Provider-specific SDK creation remains behind `IMafProviderAgentFactory`.

## Pattern Decision

Builder/coordinator plus factory and strategy for handoff.

## Testability Contract

Tests must fake capability composer/provider factory and avoid live provider credentials.

## Partial Class Policy

No partials allowed.

## Architecture Proof Required

- Direct tests for each extracted construction owner.
- Source assertion that script policy and handoff internals no longer live in the old factory.
- Handoff integration smoke.

## Acceptance Checklist

- [ ] Factory responsibility count is reduced.
- [ ] Service-locator use is removed or narrowly justified.
- [ ] Runtime build behavior remains compatible.
- [ ] Handoff smoke passes.

## Proof Required

- `proof/SB04/manifest.md`
- `proof/SB04/semantic-invariants.md`
- build and focused test transcripts.
- source assertion transcript.

## Browser Validation Logging

- N/A. Backend runtime construction only.

## Progression Gate

- SB05 and SB03 dependent work may proceed only after runtime build seams are explicit and tested.

## Suggested Agent Prompt

```text
Execute SB04 only. Decompose MafRuntimeAgentFactory by responsibility, add focused tests, keep construction behavior out of DI lambdas, and preserve handoff behavior.
```
