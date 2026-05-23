# SB01: Proof Path And Browser Classification

## Status

- Status: `Completed`
- Critical foundation: `Yes`

## Scope

- managed process output product files were ignored by implementation proof
- dotnet stdout was misclassified as browser console evidence

## Objective

Make process proof validation accept real current-run product reads and reject non-browser stdout as browser evidence.

## Covered Inputs

- `N001`
- `N005`

## Prerequisites

- Live DB run and artifact rows mapped.
- Existing browser evidence hardening bundle reviewed for overlap.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProof.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Dependency Impact

- Dispatch proof classification only.
- No process definition schema change.

## Validation Depth

- Targeted regression tests.
- Full dispatch test class.

## Implementation

- `ResolveWorkspacePathsFromToolRequest` also scans managed workspace paths and admits only current-run product output paths.
- `IsConcreteProductPath` allows scoped managed process output product paths while still rejecting generic managed artifact roots and non-product segments.
- Result-summary browser evidence refs now use strict browser evidence reference classification.
- Provider-native browser output artifact classification now requires browser-produced artifacts or scoped browser evidence reference paths.

## Implementation Steps

- Add managed process output product path recognition.
- Add strict browser evidence reference path recognition.
- Add regression tests for the live failure shapes.

## Do Not Do

- Do not classify arbitrary `.txt` process artifacts as browser evidence.
- Do not allow managed `artifacts/` roots as product implementation proof.
- Do not add product-specific proof rules.

## Acceptance Checklist

- [x] Scoped current-run product source reads satisfy implementation proof.
- [x] Dotnet stdout evidence refs do not satisfy browser console artifact requirements.
- [x] Full dispatch test class passes.

## Proof Required

- `bundle://proof/SB01/transcripts/targeted-tests.txt`
- `bundle://proof/SB02/transcripts/targeted-tests.txt`

## Browser Validation Logging

- Not applicable. This subbundle validates browser evidence classification, not a rendered frontend surface.

## Progression Gate

- Passed after targeted and full dispatch tests.

## Suggested Agent Prompt

Use `bundle://shared-prompts/implementation-prompt.md`.

## Closure

- Regression tests passed.
- Full dispatch test class passed.
- No domain-specific Tetris or Blazor process rule was added.
