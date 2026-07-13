# 04-provider-build-session-and-finalizer-drivers

## Status

- `Ready`

## Objective

Extract provider build, credential resolution, streaming dispatch, runtime session construction, hosted runtime wrapping, disposal, and finalizer coordination into focused drivers/factories while preserving runtime behavior.

## Covered Inputs

- M003, M004, M007, M009, M010
- R005, R007, R010, R012

## Prerequisites

- SB01 responsibility map.
- SB02 contracts and dependency classifications.
- SB03 capability assembly contract stable enough for runtime build integration.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionBuilder.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderRuntimeGateway.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderStreamingDispatchGate.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafFinalizerDriver.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafModelParametersBuilder.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/MafAgentRuntimeHandoffTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeProviderHealthTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs`

## Deliverables

- Provider runtime client factory seam with fakeable provider clients.
- Credential resolution/promote behavior isolated and tested with masked diagnostics.
- Runtime session factory or evolved session builder seam.
- Finalizer coordinator seam around capture, validation, recovery, and response shaping.
- Disposal/lease behavior preserved with tests.
- Integration parity tests for representative provider/session/finalizer flows.

## Dependency Impact

- SB06 depends on these seams for integration tests that mock providers and finalizers.
- SB07 depends on this phase to measure provider build/session/finalizer cost separately from capability composition.
- Later agent-specific cases depend on provider and finalizer behavior being stable and mockable.

## Validation Depth

- `Critical execution foundation`

## Implementation Steps

1. Extract provider build and credential resolution behind typed factory contracts.
2. Preserve provider-specific behavior for OpenAI, Azure OpenAI, Ollama, and any existing provider kinds.
3. Extract session construction while preserving chat history and request-source filtering behavior.
4. Extract finalizer coordination around existing `MafFinalizerDriver` where possible.
5. Preserve disposal and async-disposal behavior for runtime agents and provider resources.
6. Add direct tests with fake provider dependencies and finalizer scenarios.
7. Run integration parity tests and update proof.

## Scope Exceptions

- Do not change provider protocol semantics unless required to preserve behavior in the new seam.
- Do not optimize provider network latency; only isolate and measure local build/session cost.

## Do Not Do

- Do not expose provider credentials in logs or proof.
- Do not bypass streaming dispatch gates or approval handling.
- Do not leave finalizer behavior split between old private state and new coordinator without clear ownership.
- Do not introduce a second runtime execution path.

## Acceptance Checklist

- [ ] Provider build is fakeable in tests.
- [ ] Credential resolution preserves masking and fallback semantics where intentionally retained.
- [ ] Session construction is directly testable.
- [ ] Finalizer behavior has direct and parity tests.
- [ ] Disposal behavior is preserved.

## Proof Required

- `proof/SB04/manifest.md`
- `proof/SB04/semantic-invariants.md`
- `## Production Behavior Artifact Matrix` for runtime build result, credential diagnostics, finalizer records, and disposal paths.
- Test transcripts for direct tests and integration parity.
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.

## Browser Validation Logging

- N/A unless UI-visible provider/runtime diagnostics are added.

## Progression Gate

- SB06/SB07 may rely on this phase only after provider/session/finalizer behavior is directly testable and runtime integration parity passes.

## Suggested Agent Prompt

```text
Implement SB04 only. Extract provider build, session construction, credential/dispatch seams, and finalizer coordination behind focused drivers/factories. Preserve behavior, masking, disposal, approvals, and integration parity.
```
