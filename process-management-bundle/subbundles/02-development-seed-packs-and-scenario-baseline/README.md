# 02 Development Seed Packs And Scenario Baseline

## Status

- `Completed`

## Objective

- Define the seed architecture and named scenario packs that future implementation, integration tests, Playwright proof, and post-phase repair bundles will all reuse.

## Covered Inputs

- `REQ-012`
- `REQ-020`
- Raw notes `N07` and `N10`
- Legacy features `PRM-F12` and `PRM-F15`

## Prerequisites

- `01-canonical-ownership-and-cross-repo-convergence`

## Exact Source References

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Support\TestProfileSeedHelper.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\WorkspaceStorage.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\Placement\StoragePlacementService.cs`
- `C:\repositories\CanDoItAll.IPFS\README.md`
- `C:\repositories\CanDoItAll\process-management-bundle\inventories\03-development-seed-plan.md`

## Deliverables

- Seed-pack structure for workspace, CRM-HR, project, process-definition, runtime, and evidence layers.
- Named scenario catalog covering approval, escalation, refusal, conformance, and management review.
- Decision on how the first implementation reuses current test and storage helpers.
- IPFS-ready evidence descriptor fields reserved without forcing the first merge to depend on IPFS code.

## Dependency Impact

- Later UI and runtime proof quality depends on realistic seed data.
- If this subbundle is weak, later integration tests and Playwright flows will prove only toy paths and hide real defects.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Reuse current helper patterns and identify missing seed seams.
2. Define named scenario packs and what each one proves.
3. Define artifact and evidence seed shapes, including future IPFS descriptors.
4. Define how the same seed packs will be reused across tests, demos, and post-phase repair bundles.

## Scope Exceptions

- Implementing the final seed helpers is deferred to later execution phases, but the required seed scenarios must be locked now.

## Do Not Do

- Do not rely on random or ad hoc data factories only.
- Do not tie the first seed pass to direct IPFS runtime integration.
- Do not create separate seed stories for tests and Playwright when one scenario pack can serve both.

## Acceptance Checklist

- Foundational seed packs and scenario seeds are explicit.
- Required human, supplier, AI, and hybrid staffing cases are covered.
- Refusal, exception, and conformance scenarios are covered.
- Artifact trust and evidence-storage fields have a clear first-wave and later-IPFS path.

## Proof Required

- Updated seed inventory with scenario coverage.
- Clear reuse plan for current seed helpers and storage seams.
- Explicit identification of missing helper work to be implemented in later phases.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Later phases may not start until the scenario seed catalog is stable enough that runtime and UI proof will not invent one-off fixtures.

## Suggested Agent Prompt

```text
Implement only the development and test seed baseline for the process bundle. Reuse existing CanDoItAll helpers where possible, define named scenario packs, and reserve future IPFS evidence metadata without forcing a first-wave runtime dependency.
```
