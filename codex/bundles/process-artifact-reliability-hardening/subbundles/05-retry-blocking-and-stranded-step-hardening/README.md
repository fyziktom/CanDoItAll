# SB05 - Retry Blocking And Stranded Step Hardening

## Status

Ready

## Objective

Prevent process steps from repeating identical failed attempts when required artifacts remain missing or malformed. Route invariant artifact failures to diagnostics, manager recovery, or blocked state.

## Covered Inputs

- N001, N006, N007
- Finding F009

## Prerequisites

- SB01 finalizer exists.
- SB02 diagnostics exist.
- SB03 recovery is evidence-bound.
- SB04 projection safety is complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Artifact failure fingerprinting.
- Retry decision model that distinguishes transient executor failure from invariant artifact contract failure.
- Stranded step recovery that uses diagnostics and recoverable execution run state.
- Blocked state reason that names missing/invalid artifacts and next action.
- No blind 5x retry loop for unchanged artifact failure.

## Dependency Impact

Final runtime behavior improvement. Depends on earlier validation/recovery/projection correctness.

## Validation Depth

High. Tests must simulate repeated identical missing-artifact failure and prove only one targeted recovery/blocking path is taken.

## Implementation Steps

1. Define artifact failure fingerprint from expectation, failure kind, expected path/mode/schema, and attempted source.
2. Persist or derive recent fingerprints for a step attempt window.
3. Update retry policy to stop when the same artifact failure repeats without new evidence or changed recovery context.
4. Route invariant failures to manager recovery once when eligible.
5. Block with exact diagnostic when recovery is not eligible or fails.
6. Keep normal retries for transient provider/tool failures where a retry can change the result.

## Scope Exceptions

- Do not remove all retries. Only stop invariant artifact contract retries.
- Do not mask provider/tool failures as artifact failures unless diagnostics prove that is the root cause.

## Do Not Do

- Do not simply lower retry count globally.
- Do not return Completed with missing or invalid artifacts.
- Do not repeatedly ask the same executor to write the same missing artifact after diagnostics prove it failed unchanged.

## Acceptance Checklist

- [ ] Same missing artifact failure fingerprint does not cause repeated executor retries.
- [ ] Same invalid format fingerprint does not cause repeated executor retries.
- [ ] Transient tool/provider failure can still retry when appropriate.
- [ ] Stranded step recovery uses finalizer diagnostics.
- [ ] Blocked reason names exact missing/invalid artifacts.

## Proof Required

- `proof/SB05/manifest.md`
- `proof/SB05/semantic-invariants.md`
- repeated-fingerprint tests
- stranded recovery tests
- source assertions for retry decision integration
- changed-file hashes

## Progression Gate

Do not start SB06 until repeated invariant artifact failures are demonstrably non-looping.

## Browser Validation Logging

N/A unless this subbundle adds or changes browser-visible UI. If browser proof is needed for a process scenario, record route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Suggested Agent Prompt

Use the shared implementation prompt at `bundle://shared-prompts/implementation-prompt.md`, then append this subbundle README and the exact source references above. Execute only this subbundle. Record proof before moving on.
