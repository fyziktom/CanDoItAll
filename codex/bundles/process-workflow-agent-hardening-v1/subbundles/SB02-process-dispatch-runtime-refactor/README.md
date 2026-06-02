# SB02 - Process Dispatch And Runtime Refactor

## Status

Completed. Classification: **Critical foundation**.

## Objective

Refactor process dispatch/runtime responsibility centers around canonical contracts while preserving existing behavior. Strengthen current-run lineage, artifact validation, state transitions, finalization, concurrency, and cancellation.

## Covered Inputs

Covers large process dispatch files, stale lineage, artifact proof fragility, status/transition canonicity, `CancellationToken.None` paths, dispatch guards, and QA recovery hardening.

## Prerequisites

SB01 completed with canonical descriptors and drift scanner baseline.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs`

## Deliverables

- Extracted process policy/evaluator/finalizer/projection services.
- Characterization tests for existing successful path.
- Current-run lineage validator.
- Artifact acceptance validator that rejects stale run ids and unrelated artifact roots.
- Cancellation-token audit and fixes in owned process dispatch paths.
- Proof manifest and semantic invariants for SB02.

## Dependency Impact

SB04 browser proof, SB07 UI display, SB08 E2E, and SB09 final red-team depend on process lineage and state-transition correctness.

## Validation Depth

Deep semantic validation with failing-first tests for stale lineage and positive tests for current-run artifact acceptance. Include one dependent-flow smoke at API/detail level.

## Implementation Steps

1. Add characterization tests for current artifact satisfaction and step completion behavior.
2. Extract pure services from dispatch partials without behavior changes.
3. Introduce lineage/current-run binding validator.
4. Reject artifacts that belong to stale process run id, stale execution run id, wrong project id, or wrong artifact root.
5. Replace local magic ids with SB01 descriptors.
6. Remove avoidable `CancellationToken.None` in owned paths.
7. Keep compatibility adapters for current templates and API DTOs.
8. Run targeted process tests and solution build/test gate.

## Scope Exceptions

UI changes are limited to what is necessary for compile compatibility. Full UI display hardening belongs to SB07.

## Do Not Do

- Do not change process template semantics unless tests and migration notes are included.
- Do not hide stale-lineage failures as warnings.
- Do not accept process artifacts based only on title/path.
- Do not convert dispatch into a new monolith.

## Acceptance Checklist

- [x] Characterization tests pass before and after extraction.
- [x] Stale run id artifact is rejected.
- [x] Current run artifact is accepted.
- [x] State transition behavior remains compatible.
- [x] Cancellation-token audit is documented.
- [x] Drift scanner shows no new unowned ids.
- [x] SB02 proof manifest exists.

## Proof Required


Because this is a critical subbundle, the Semantic Adequacy Gate proof must include:

- `proof/SBxx/manifest.md`
- `proof/SBxx/semantic-invariants.md` or `.json`
- changed-file hashes
- command transcript paths
- source assertions
- shallow-pass trap
- adversarial negative proof
- semantic positive proof
- anti-stub audit
- raw-note literal closure
- dependency smoke proof where stated

Production Behavior Artifact Matrix required for any new artifact lineage state, event, validator record, or process transition diagnostic.


## Browser Validation Logging

N/A unless a browser-visible process dashboard is changed incidentally. If changed, record route, viewport, screenshot, console, and result.

## Progression Gate

SB02 passes only when process artifact/state behavior is proven semantically and stale-run proof is rejected by tests.

## Suggested Agent Prompt

Implement SB02 only. Refactor process dispatch services around SB01 contracts, prove current-run lineage, and do not change UI beyond compile-compatible adjustments.
