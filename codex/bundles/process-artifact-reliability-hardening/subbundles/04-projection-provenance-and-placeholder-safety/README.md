# SB04 - Projection Provenance And Placeholder Safety

## Status

Ready

## Objective

Harden artifact projection so stale files, placeholders, gap markers, subprocess proxy records, and weak managed-file imports cannot falsely satisfy required process artifact expectations.

## Covered Inputs

- N001, N006
- Findings F007, F008 and prior subprocess/placeholder concern

## Prerequisites

- SB01 finalizer exists.
- SB02 artifact validation exists.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Current-run validation for existing managed artifact projection.
- Placeholder/gap records represented as diagnostics, not satisfying artifacts.
- Subprocess artifact projection verified and hardened.
- Provider-native browser output projection includes current-run/source validation.
- Projection result returns explicit recorded/satisfied/diagnostic state.

## Dependency Impact

Supports SB05 retry correctness by ensuring the runtime knows the difference between “missing”, “placeholder”, and “valid”.

## Validation Depth

High. Add negative tests for stale managed files, placeholder records, subprocess missing child artifact, and provider-native browser scratch outputs.

## Implementation Steps

1. Inspect subprocess projection code paths not fully covered in this bundle's initial source scan.
2. Add tests proving missing child/subprocess artifacts do not satisfy parent required expectations.
3. Add current-run or explicit carry-forward validation to existing managed artifact projection.
4. Represent gap/placeholder states through diagnostics/provenance, not satisfied expectation ids.
5. Tighten provider-native browser output projection to distinguish current-run outputs from scratch/stale files.
6. Return explicit projection results to the finalizer.

## Scope Exceptions

- Do not block optional artifacts because of stricter required artifact rules.
- Do not remove useful projection sources; guard them with validation instead.

## Do Not Do

- Do not create a `ProcessArtifactRecord` with a required expectation id for a missing child artifact.
- Do not let stale files in the managed workspace satisfy current-run expectations without carry-forward proof.

## Acceptance Checklist

- [ ] Stale existing managed file does not satisfy current required expectation.
- [ ] Placeholder/gap marker does not satisfy required expectation.
- [ ] Subprocess missing child artifact blocks or creates diagnostic, not parent completion.
- [ ] Provider-native browser output is current-run validated.
- [ ] Projection result is explicit and consumed by finalizer.

## Proof Required

- `proof/SB04/manifest.md`
- `proof/SB04/semantic-invariants.md`
- failing-first and passing tests for stale/placeholder/subprocess scenarios
- source assertions for projection result integration
- changed-file hashes

## Progression Gate

Do not start SB05 until projection cannot falsely satisfy required expectations.

## Browser Validation Logging

N/A unless this subbundle adds or changes browser-visible UI. If browser proof is needed for a process scenario, record route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Suggested Agent Prompt

Use the shared implementation prompt at `bundle://shared-prompts/implementation-prompt.md`, then append this subbundle README and the exact source references above. Execute only this subbundle. Record proof before moving on.
