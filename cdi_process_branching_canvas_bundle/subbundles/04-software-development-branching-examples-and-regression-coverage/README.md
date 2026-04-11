# Software Development Branching Examples And Regression Coverage

## Status

- `Ready`

## Objective

- Seed and prove realistic software-development branching scenarios that exercise the refined branch-node model, including review repair loops, QA loops, join-style evidence aggregation, and layout-persistence round trips.

## Covered Inputs

- `N009` Proper branching examples around software development.
- `N013` Many-to-many routing semantics must be supported or blocked honestly.
- `N014` Moved derived nodes must persist and not snap back after later interactions.

## Prerequisites

- `subbundles/01-scenario-definition-and-live-gap-reconciliation` must be `Completed` and trusted.
- `subbundles/02-advanced-canvas-node-contract` must be `Completed` and trusted.
- `subbundles/03-process-branch-node-authoring-and-mapping` must be `Completed` and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDevelopmentSeedService.Scenarios.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDevelopmentSeedService.RuntimeSeeds.Complex.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`

## Deliverables

- At least one seeded software-development process that visibly demonstrates branch routing, join-style input aggregation or the honest blocker for it, and persisted layout stability.
- Regression coverage that proves the seeded semantics remain correct.
- Trouble-log updates if seeded scenarios reveal new canonical gaps.

## Dependency Impact

- Final browser closure depends on this phase because the user explicitly asked for realistic software-development scenarios, not only infrastructure.
- Weak proof here would leave the feature technically present but semantically unproven on the requested join and persistence cases.

## Validation Depth

- `UI, integration-test, and browser-proof`

## Implementation Steps

1. Update seeded process scenarios to reflect the target inventory from subbundle `01`.
2. Add or extend tests to cover the refined scenario semantics, especially join-style inputs and persistence round trips when supported.
3. Ensure the process workspace can load and display the new seeded examples.
4. Revisit the architecture trouble log if the scenarios reveal missing semantics or storage gaps.
5. Validate the seeded scenarios in the browser and capture screenshots.

## Do Not Do

- Do not add generic toy examples that avoid repair loops, joins, or persistence.
- Do not close this phase with browser-only scenarios that the canonical model still cannot round-trip.

## Acceptance Checklist

- The seeded examples include at least one repair loop and one QA loop.
- The seeded examples include at least one join-style input case or explicitly document why the current model blocks it.
- Regression tests cover the new scenarios or the same semantics they exercise.
- The architecture trouble log is updated if the examples reveal new gaps.

## Proof Required

- Focused integration or component tests for seeded branching semantics.
- Browser screenshots on `/processes` showing at least one software-development example with visible loop or join behavior.

## Browser Validation Logging

- Route: `/processes`
- Viewports: `Large-screen desktop` and `1280x800`
- Playwright MCP actions: navigate, load the seeded example, inspect branch-node loops or joins, capture screenshots
- Expected evidence path: example-scenario screenshots recorded in `reviews/01-execution-report.md`

## Progression Gate

- Final closure may continue only after at least one realistic software-development example is visible in the browser and supported by regression coverage.

## Suggested Agent Prompt

```text
Implement this subbundle only. Update the seeded software-development scenarios so they exercise review routing, QA loops, join-style inputs when canonically supported, and layout persistence expectations, back them with regression coverage, and prove at least one refined scenario visibly in the browser.
```
