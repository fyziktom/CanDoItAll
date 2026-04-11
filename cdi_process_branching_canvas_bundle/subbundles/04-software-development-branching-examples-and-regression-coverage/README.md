# Software Development Branching Examples And Regression Coverage

## Status

- `Completed`

## Objective

- Seed and prove realistic software-development branching scenarios that exercise the new branch-node model, including repair loops, QA loops, and final merge readiness.

## Covered Inputs

- `N009` Proper branching examples around software development.
- `N010` Architecture troubles must remain visible as scenarios expose missing pieces.

## Prerequisites

- `subbundles/01-scenario-definition-and-live-gap-reconciliation` must be `Completed` and trusted.
- `subbundles/02-advanced-canvas-node-contract` must be `Completed` and trusted.
- `subbundles/03-process-branch-node-authoring-and-mapping` must be `Completed` and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDevelopmentSeedService.Scenarios.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDevelopmentSeedService.RuntimeSeeds.Complex.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs`

## Deliverables

- At least one seeded software-development process that visibly demonstrates branch loops around code review, repair, QA, and merge readiness.
- Regression tests that prove branch routing still behaves correctly under realistic example data.
- Any newly discovered architecture trouble recorded back into the trouble log.

## Dependency Impact

- Final browser proof depends on this phase because the user explicitly asked for realistic branching examples, not only infrastructure.
- Weak proof here would leave the feature technically present but semantically unproven on the requested scenarios.

## Validation Depth

- `UI, integration-test, and browser-proof`

## Implementation Steps

1. Implement or update seeded process scenarios to reflect the target inventory from subbundle `01`.
2. Add or extend integration tests to cover loop routing and approval outcomes on realistic software-development flows.
3. Ensure the process workspace can load and display the new seeded examples.
4. Revisit the architecture trouble log if the scenarios reveal missing semantics.
5. Validate the seeded scenarios in the browser and capture screenshots.

## Scope Exceptions

- If a scenario cannot be seeded cleanly in the current development environment, record the exact blocker and create a concrete follow-up instead of removing the scenario silently.

## Do Not Do

- Do not add generic toy examples that avoid repair or QA loops.
- Do not close this phase with linear scenarios that fail to exercise the new branch-node ports.

## Acceptance Checklist

- The seeded examples include at least one repair loop and one QA loop.
- The seeded examples visibly use branch nodes and branch-node routes.
- Regression tests cover the new examples or the same semantics they exercise.
- The architecture trouble log is updated if the examples reveal new gaps.

## Proof Required

- Focused integration tests for seeded branching semantics.
- Browser screenshots on `/processes` showing at least one software-development example with visible loop branches.

## Browser Validation Logging

- Route: `/processes`
- Viewports: `1600x900` and `1280x800`
- Playwright MCP actions: navigate, load the seeded example, inspect branch-node loops, capture screenshots
- Expected evidence path: example-scenario screenshots recorded in `reviews/01-execution-report.md`
- Screenshot review questions: can the loop paths be followed visually, are review and QA routes distinguishable, and does the canvas remain readable once multiple branch paths are visible

## Progression Gate

- Final closure may continue only after at least one realistic software-development example is visible in the browser and supported by regression coverage.

## Suggested Agent Prompt

```text
Implement this subbundle only. Add realistic software-development branching examples that exercise review, repair, QA, and merge approval loops, back them with regression coverage, and prove at least one seeded scenario visibly in the browser.
```

## Closure Notes

- A new seeded scenario, `Branching code review and merge governance`, now exercises review routing, repair, QA, security, architecture escalation, merge approval, default handling, and error handling on one branch-heavy software-development canvas.
- The baseline-seeding integration test was updated and passed with the new scenario present.
- The scenario is intentionally branch-heavy rather than truly cyclic, because the current process model still lacks first-class loop-back and multi-parent join semantics. That gap is recorded in the architecture trouble log.
