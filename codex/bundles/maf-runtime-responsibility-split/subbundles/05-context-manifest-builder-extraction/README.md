# Context Manifest Builder Extraction

## Status

- `Completed`

## Objective

- Move context assembly manifest creation and token/schema estimates into a dedicated context manifest builder.

## Covered Inputs

- N008
- Requirements R08, R09, R10

## Prerequisites

- SB01 closure gate passed.
- SB02 closure gate passed.
- Current manifest shape is characterized.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafContextManifestBuilder.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Runtime/AgentRuntimeContextAssemblyModels.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Context.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs`

## Deliverables

- Internal context manifest builder.
- Tests for included/excluded source records, totals, token estimates, and tool schema character estimates.
- Runtime capability/context code updated to use the builder.

## Dependency Impact

- Capability composition tests and execution run tracking depend on manifest shape and totals.
- SB07 can only slim runtime after context-manifest behavior is delegated.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add direct tests for current manifest output.
2. Extract builder without changing manifest record types.
3. Keep estimate helpers deterministic and covered.
4. Update runtime call sites.
5. Run focused unit and integration tests.

## Scope Exceptions

- Do not redesign context contribution policy or capability attachment.

## Do Not Do

- Do not change manifest source names or reason strings without tests and explicit acceptance.
- Do not move capability filtering decisions into the manifest builder.

## Acceptance Checklist

- Manifest construction no longer lives in a `MafAgentRuntime` partial.
- Tests directly exercise builder output.
- Context source totals remain compatible with existing integration tests.

## Proof Required

- `proof/SB05/manifest.md`
- `proof/SB05/semantic-invariants.md`
- Characterization or failing-first transcript.
- Passing unit and integration test transcripts.
- Source assertions showing runtime uses the context manifest builder.
- Changed-file hashes.
- Anti-stub audit.
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.
- If this subbundle introduces a production signal, state, record, or event, add a Production Behavior Artifact Matrix to both proof artifacts.

## Browser Validation Logging

- Deferred to SB08. Browser-visible context diagnostics, if changed, must be added to the SB08 route assertions.

## Progression Gate

- SB07 may start only after manifest tests and execution tracking integration tests pass.

## Suggested Agent Prompt

```text
Implement SB05 only. Extract context manifest construction into a focused builder, preserve manifest shape and estimates, capture proof under proof/SB05, and stop if capability policy starts moving into the builder.
```
