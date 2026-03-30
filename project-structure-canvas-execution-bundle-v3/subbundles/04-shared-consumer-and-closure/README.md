# 04 Shared Consumer And Closure

## Status

- Status: `Completed`
- Legacy task coverage: `T16-T17`

## Objective

Prove PromptFactory compatibility, archive the final evidence, reduce misleading dead runtime paths, and close the bundle through the current validator.

## Covered Inputs

- `R04`
- `R05`
- `R06`
- `R07`
- `R08`

## Prerequisites

- `03-runtime-renderer-migration` is completed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\PromptLibraryVerificationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`
- `C:\repositories\CanDoItAll\project-structure-canvas-execution-bundle-v3\reviews\01-execution-report.md`

## Deliverables

- Shared-consumer validation for PromptFactory.
- Final execution report, raw-note closure, and self-review updates.
- Passing validator compatibility layer for the current bundle workflow.

## Dependency Impact

- Closes the bundle only after shared consumers, documentation, and validator gates all agree with the shipped code.

## Validation Depth

- PromptFactory browser proof.
- Full component and Playwright regression packs.
- Bundle validator prepared and completed stages.

## Implementation Steps

- Validate PromptFactory state and toolbox behavior on the shared renderer.
- Remove misleading dead runtime paths where they are clearly unreferenced and safe to delete.
- Add the normalized validator compatibility layer without replacing the original legacy bundle archive.
- Update bundle status documents only after the final regression pack is green.

## Do Not Do

- Do not call the bundle closed while the execution report, self-review, validator result, and repository state disagree.

## Acceptance Checklist

- PromptFactory browser scenarios are green.
- Component and Playwright packs are green.
- Execution report and raw-note closure are final.
- Prepared and completed validator stages pass.

## Proof Required

- `PromptFactoryBrowserTests.Prompt_factory_canvas_surface_loads_and_exposes_shared_chrome`
- `PromptFactoryArtifactCaptureTests.Prompt_factory_artifacts_capture_toolbox_preview_and_single_add_flow`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --nologo`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --nologo`

## Browser Validation Logging

- Route: `/prompt-factory`
- Viewport: `1900x1200`
- Evidence: `output/playwright/bundle-p0-07-prompt-factory-diagnostics.png`, `artifacts/screenshots/i21`, `artifacts/screenshots/i22`, `artifacts/screenshots/i24`

## Progression Gate

- Passed because shared-consumer proof, final regression proof, and validator closure proof all align with the current repository state.

## Suggested Agent Prompt

Close the bundle only if PromptFactory, the final regression pack, the execution report, and the validator gates all agree with the shipped code.
