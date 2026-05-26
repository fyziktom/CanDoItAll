# SB05: Replace reason-text block inference with typed causes.

## Objective

Replace reason-text block inference with typed causes.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Add typed block cause to `ProcessStepTransitionRequest` or a parallel transition metadata object.
- Make finalizer pass `OwnOutput`, `UpstreamInput`, `RuntimeEvidence`, or `PolicyDenied` cause explicitly.
- Fix `ProcessStepRunBlockState.InferBlockReasonCode` fallback so own required artifact failure is not classified as missing upstream artifact.
- Add tests for own missing artifact vs upstream missing artifact.
- Ensure recovery options differ correctly for own-output artifact recovery and upstream materialization.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.

## Status

- Completed

## Covered Inputs

- RN06 infer wrong block/recovery classification from broad reason text.
- RQ04 typed block cause propagation.
- RQ10 executable recovery router.

## Prerequisites

- SB04 closure gate passes.
- Shared completion validation from SB03 remains trusted.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessStepRunBlockState.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs

## Deliverables

- Typed block cause passed at block creation instead of inferred from broad reason text.
- Own-output artifact failure and upstream-input materialization failure produce distinct block codes and recovery options.
- Legacy text inference remains only as a fallback.

## Dependency Impact

- SB10 recovery router depends on typed causes.
- SB11 classifier extraction depends on this behavior being stable.
- SB13 diagnostics consume typed block and recovery state.

## Validation Depth

- Negative test where own missing artifact must not become missing upstream artifact.
- Positive test where upstream input still produces materialization recovery.
- Source assertion for finalizer failure ownership propagation.

## Implementation Steps

- Add transition metadata or request field for typed block cause.
- Pass finalizer `FailureOwnership` into step block state.
- Adjust fallback inference so own required artifact failures do not map to upstream materialization.
- Assert recovery option differences in tests.
- Record proof under `bundle://proof/SB05/`.

## Do Not Do

- Do not parse finalizer diagnostic text when typed ownership is available.
- Do not collapse own-output, upstream-input, runtime-evidence, and policy-denied causes into one code.
- Do not hide legacy fallback as the primary behavior.

## Acceptance Checklist

- Own-output missing artifact maps to artifact contract recovery.
- Upstream missing artifact maps to materialization recovery.
- Finalizer and manual/API block paths carry typed cause where available.
- Focused tests pass.

## Proof Required

- `bundle://proof/SB05/manifest.md`
- `bundle://proof/SB05/semantic-invariants.md`
- Failing-first or red-team transcript.
- Passing focused test transcript.
- Changed-file SHA-256 transcript.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A: SB05 changes runtime classification only.

## Progression Gate

- SB06 and SB10 may proceed only after typed block causes are persisted and recovery options differ by ownership.

## Suggested Agent Prompt

- Implement SB05 typed cause propagation, update `proof/SB05`, run focused process tests, and record gate closure before recovery-router work.
