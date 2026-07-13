# Model Parameters Builder Extraction

## Status

- `Completed`

## Objective

- Move model-compatible chat option construction and model parameter diagnostics into a dedicated builder.

## Covered Inputs

- N007
- Requirements R07, R09, R10

## Prerequisites

- SB01 closure gate passed.
- SB02 closure gate passed.
- Current model-parameter behavior is characterized.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafModelParametersBuilder.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeImageAnalysisModelTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeProviderHealthTests.cs`

## Deliverables

- Internal model parameters builder or options factory.
- Direct tests for temperature omission, temperature retry recognition, unsupported exception matching, reasoning effort mapping, runtime model resolution, and diagnostics text.
- Runtime call sites updated to use the builder.

## Dependency Impact

- Provider execution and image-analysis model behavior depend on these options.
- SB07 cannot prove runtime slimming while model policy remains mixed into the runtime.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add direct tests around current `CreateModelCompatibleChatOptions` behavior.
2. Extract builder with explicit typed inputs.
3. Keep unsupported transport diagnostics stable.
4. Update runtime call sites.
5. Run focused unit tests and MAF build.

## Scope Exceptions

- Do not change provider model selection policy beyond preserving current behavior.

## Do Not Do

- Do not hardcode provider names in new branches unless they already exist and are migrated into constants/policies.
- Do not swallow unsupported option exceptions.
- Do not add retry behavior outside the existing explicit retry path.

## Acceptance Checklist

- `MafAgentRuntime.ModelParameters.cs` no longer owns builder logic.
- The builder is directly testable without creating a full `MafAgentRuntime`.
- Existing provider/image model tests pass.
- Diagnostics remain actionable.

## Proof Required

- `proof/SB04/manifest.md`
- `proof/SB04/semantic-invariants.md`
- Characterization or failing-first transcript.
- Passing unit-test transcript.
- MAF project build transcript.
- Source assertions proving runtime delegates to model builder.
- Changed-file hashes.
- Anti-stub audit.
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.
- If this subbundle introduces a production signal, state, record, or event, add a Production Behavior Artifact Matrix to both proof artifacts.

## Browser Validation Logging

- Deferred to SB08. If model diagnostics become UI-visible during implementation, add the affected route to SB08 analytics.

## Progression Gate

- SB07 may start only after model option tests and MAF build pass.

## Suggested Agent Prompt

```text
Implement SB04 only. Extract model parameter construction into a focused builder, preserve exact compatibility and diagnostic behavior, capture proof under proof/SB04, and stop if provider selection policy starts changing.
```
