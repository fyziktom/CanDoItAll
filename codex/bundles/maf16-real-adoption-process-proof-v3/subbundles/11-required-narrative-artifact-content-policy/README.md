# SB11: 11-required-narrative-artifact-content-policy

## Goal

Add explicit content policy for strict required narrative artifacts.

## Required work

- Define when Narrative/Decision artifacts must be content-backed.
- For strict process definitions, required Brief/Artifact contract records with managed path should require readable content unless marked manual/no-file.
- Add validation status and API display for RecordedButContentUnavailable or equivalent.
- Test first-step delivery contract with and without readable content.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB11` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Make required content-backed narrative artifacts fail predictably when stored content is unreadable.

## Covered Inputs

- RQ08 narrative artifact content policy.

## Prerequisites

- SB10 dedupe scope proof passes.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- `ContentUnavailable` validation for required content-backed narrative artifacts.

## Dependency Impact

- SB13 read-model parity consumes this typed status.

## Validation Depth

- Failing-first and passing integration test proof.

## Implementation Steps

- Add an explicit stored-content requirement helper.
- Use it in artifact format/content validation.
- Add the missing-content regression.

## Do Not Do

- Do not silently mark an unreadable required artifact as satisfied.

## Acceptance Checklist

- Missing content returns `ContentUnavailable` and `IsSatisfied` is false.

## Proof Required

- `proof/SB11/manifest.md` and `proof/SB11/semantic-invariants.md`.

## Browser Validation Logging

- No browser route is affected.

## Progression Gate

- Content policy proof must pass before read-model parity is closed.

## Suggested Agent Prompt

Treat required content-backed narrative evidence as invalid when the managed content cannot be loaded.
