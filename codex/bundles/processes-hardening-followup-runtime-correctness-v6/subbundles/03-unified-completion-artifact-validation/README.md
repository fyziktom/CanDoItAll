# SB03: Use finalizer-grade artifact validation for manual/API transitions.

## Objective

Use finalizer-grade artifact validation for manual/API transitions.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Extract finalizer artifact validation into a reusable service or static validator.
- Replace `TransitionStepAsync` local `ValidateRequiredArtifactsForCompletion` with the shared validator.
- Ensure manual transitions reject placeholder, malformed JSON, stale/wrong-run, wrong producer, and missing storage content.
- Ensure exception/repair branch policy is typed, not only branch-title text.
- Add tests for manual completion with malformed JSON and placeholder artifact.

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

- RN02 complete with weak/manual artifact validation.
- RQ03 unified completion validation.

## Prerequisites

- SB02 closure gate passes for artifact identity.
- Prepared-stage bundle validator passes.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs

## Deliverables

- Reusable completion artifact validator shared by automated finalizer and manual/API transition.
- Manual/API rejection of placeholder, malformed JSON, wrong-run, wrong producer, and missing content artifacts.
- Typed exception/repair branch routing.

## Dependency Impact

- SB07 refactoring depends on the validator shape.
- SB08 storage reader must plug into the same shared validator.
- SB14 final red-team scenarios depend on parity between automated and manual completion.

## Validation Depth

- Failing-first tests for manual malformed JSON and placeholder artifacts.
- Passing tests for valid manual completion through the shared validator.
- Source assertions proving finalizer and manual/API transitions call the same validation path.

## Implementation Steps

- Extract or expose finalizer-grade validation through a shared service/static validator.
- Replace local `ValidateRequiredArtifactsForCompletion` weak checks.
- Preserve skip-own-output fast paths only where contracts explicitly allow them.
- Add regression tests for malformed content and placeholder artifacts.
- Record proof under `bundle://proof/SB03/`.

## Do Not Do

- Do not keep two divergent validators for finalizer and manual transitions.
- Do not use branch-title string matching where typed disposition is available.
- Do not weaken artifact validation for compatibility without a warning and test.

## Acceptance Checklist

- Manual/API completion rejects invalid stored content and placeholders.
- Automated finalizer and manual transition share validation semantics.
- Focused integration tests pass.
- Checkpoint A can start with a stable validator boundary.

## Proof Required

- `bundle://proof/SB03/manifest.md`
- `bundle://proof/SB03/semantic-invariants.md`
- Failing-first transcript for manual bypass.
- Passing focused test transcript.
- Changed-file SHA-256 transcript.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A: SB03 changes runtime validation paths only.

## Progression Gate

- SB04 may start only after shared completion validation is proven and no local weak manual validator remains.

## Suggested Agent Prompt

- Implement SB03 with the smallest shared-validator extraction, update `proof/SB03`, run focused process tests, and record SB03 gate closure.
