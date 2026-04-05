# 05-service-decomposition-guardrail-tests-and-final-review

## Status

- `Completed`

## Objective

Break the Workbench hotspot into focused services, add architecture guardrail tests, and rerun the canonical review before reopening the plugin wave.

## Covered Inputs

- `PWA-008`
- `R-001`
- `R-002`
- `R-004`
- `R-006`
- `R-007`

## Prerequisites

- SB01 through SB04 complete.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`

## Evidence Focus

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:343-424`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:662-749`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:944-1135`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1962-2239`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:1262-1425`

## Deliverables

- Decomposed services with narrower responsibilities.
- Architecture guardrail tests for canonical truth, metadata ownership, registry usage, and plugin seams.
- Fresh post-wave canonical review and scorecard.
- Full build/test proof in a real .NET environment.

## Dependency Impact

- This is the release gate for the plugin wave.
- It makes future review cycles cheaper and more reliable.

## Validation Depth

- Full build/test in the real environment.
- Targeted integration/component/playwright reruns for affected areas.
- Architecture test suite plus updated scorecard.

## Implementation Steps

- Split Workbench orchestration into load/assembly, command, relation, lifecycle, and plugin-integration services.
- Add architecture tests that fail when forbidden patterns reappear (parallel truth, metadata ownership leakage, enum/switch plugin growth, giant service growth).
- Run the existing canonical-model-review skill again after implementation and refresh the repo bundle/review artifacts.

## Do Not Do

- Do not claim plugin-wave readiness without a fresh canonical review.
- Do not leave the new abstractions untested.

## Acceptance Checklist

- [x] No single service replaces ProjectWorkbenchModels.cs with the same god-class shape.
- [x] Architecture tests protect the newly established boundaries.
- [x] A final review explicitly reopens the plugin wave gate.

## Proof Required

- Build/test logs from a real .NET environment.
- Fresh bundle/review artifacts.
- Architecture test results.

## Browser Validation Logging

- Captured with `output/playwright/feedback-bundle-visuals`, `output/playwright/feedback-bundle-mutations`, `output/playwright/feedback-bundle-transfer`, and the project-assignment sync browser flow.

## Progression Gate

- Passed. Plugin wave may proceed under the guarded-rollout verdict recorded in `analysis/04-plugin-wave-readiness.md`.

## Suggested Agent Prompt

Implement SB05 by decomposing the Workbench hotspot, adding architecture guardrail tests, and rerunning the canonical-model review. Treat this subbundle as the final gate before email/LinkedIn/custom API plugins.
