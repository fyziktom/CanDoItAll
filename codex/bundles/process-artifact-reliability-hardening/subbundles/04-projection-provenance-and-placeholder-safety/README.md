# SB04 - Projection Provenance And Placeholder Safety

## Status

- Completed

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

- Supports `SB05` retry correctness.
- Ensures the runtime distinguishes missing, placeholder, stale, proxy, and valid artifacts before retry or blocking decisions.

## Validation Depth

- High validation depth is required.
- Add negative tests for stale managed files, placeholder records, subprocess missing child artifacts, and provider-native browser scratch outputs.

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

- [x] Stale existing managed file does not satisfy current required expectation unless current execution provenance matches.
- [x] Placeholder/gap marker does not satisfy required expectation.
- [x] Subprocess/proxy missing child artifacts are caught by finalizer evidence/path validation instead of parent completion.
- [x] Provider-native browser output is current-run validated through execution-run provenance and storage-path checks.
- [x] Projection result is consumed by finalizer validation before completion.

## Closure Proof

- Manifest: `bundle://proof/SB04/manifest.md`
- Semantic invariants: `bundle://proof/SB04/semantic-invariants.md`
- Passing transcript: `bundle://proof/SB04/transcripts/passing.txt`
- Source assertions: `bundle://proof/SB04/transcripts/source-assertions.txt`
- Focused regression suite: `bundle://proof/SB06/transcripts/focused-integration-tests.txt`

## Proof Required

- `proof/SB04/manifest.md`
- `proof/SB04/semantic-invariants.md`
- failing-first and passing tests for stale/placeholder/subprocess scenarios
- source assertions for projection result integration
- changed-file hashes

## Progression Gate

- Do not start `SB05` until projection cannot falsely satisfy required expectations.
- The gate must include stale, placeholder, subprocess, and provider-native browser projection proof.

## Browser Validation Logging

- N/A unless this subbundle adds or changes browser-visible UI.
- If browser proof is needed for a process scenario, record route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Suggested Agent Prompt

Use the shared implementation prompt at `bundle://shared-prompts/implementation-prompt.md`, then append this subbundle README and the exact source references above. Execute only this subbundle. Record proof before moving on.
