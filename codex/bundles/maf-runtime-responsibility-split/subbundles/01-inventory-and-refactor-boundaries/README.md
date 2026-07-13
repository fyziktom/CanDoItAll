# Inventory And Refactor Boundaries

## Status

- `Completed`

## Objective

- Convert the prepared inventory into an implementation-time responsibility map and threshold contract before any production refactor starts.

## Covered Inputs

- N001, N002, N006, N009, N010
- Requirements R01, R02, R09, R11

## Prerequisites

- Bundle prepared-stage validation has passed.
- `bundle://bundle-checklists.xlsx` is available and reviewed.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafModelParametersBuilder.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafContextManifestBuilder.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeAttachmentTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeImageAnalysisModelTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeProviderHealthTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolInvocationResultTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRecoveryIntegrationTests.cs`

## Deliverables

- Update the inventory if the repo changed since preparation.
- Decide exact line-count and catch-all thresholds for SB07.
- Identify missing characterization tests before extraction.
- Add tests only if required to make later refactors safe.

## Dependency Impact

- SB02-SB08 depend on this inventory. If it misses a responsibility, later extraction proof can pass while behavior is still split incorrectly.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Rerun line-count and symbol scans for the MAF runtime directory.
2. Compare current method clusters with `analysis/01-current-state.md`.
3. Update workbook checklist rows if any responsibility or test surface changed.
4. Identify missing characterization tests for hash formatting, session compatibility, model options, context manifest, and finalizer behavior.
5. Add only the characterization tests required before extraction.
6. Record exact SB07 thresholds for max `MafAgentRuntime.cs` lines and max new collaborator file size.

## Scope Exceptions

- Do not refactor production runtime code in this subbundle unless required to add a test seam and explicitly documented.

## Do Not Do

- Do not start helper, builder, or finalizer extraction.
- Do not redefine public runtime contracts.
- Do not weaken the requested split into more partial classes only.

## Acceptance Checklist

- Inventory is current against the repo.
- Thresholds are explicit and recorded in the execution report.
- Characterization gaps are either closed by tests or documented as prerequisites for a later subbundle.
- Workbook checklist status reflects the updated inventory.

## Proof Required

- `proof/SB01/manifest.md`
- `proof/SB01/semantic-invariants.md`
- Line-count transcript.
- Symbol scan transcript for `MafAgentRuntime`, `ComputeStableHash`, `FormatArgumentValue`, and finalizer methods.
- Test transcript for any characterization tests added.
- Source assertions mapping responsibilities to subbundles.
- Anti-stub audit if tests or helpers are added.
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.
- If this subbundle introduces a production signal, state, record, or event, add a Production Behavior Artifact Matrix to both proof artifacts.

## Browser Validation Logging

- N/A. This subbundle is inventory and test-boundary work only.

## Progression Gate

- Downstream subbundles may start only after the updated inventory, thresholds, and characterization proof are recorded in `reviews/01-execution-report.md`.

## Suggested Agent Prompt

```text
Implement SB01 only. Refresh the MAF runtime inventory, set concrete refactor thresholds, add only missing characterization tests needed before extraction, capture proof under proof/SB01, update the workbook and execution report, and stop before production refactoring.
```
