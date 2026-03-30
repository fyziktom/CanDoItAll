# Validation and closure

## Status

- `Completed`

## Objective

- Regenerate the final CanvasLib asset set, prove the structure and calendar routes still work, and close the task only if the CanvasLib folder structure is logical and no file anywhere under the package exceeds 2000 lines.

## Covered Inputs

- `N004 Validate logical structure and no CanvasLib file above 2000 lines`
- `R03`
- `R05`
- `R07`
- `R09`
- `R10`

## Prerequisites

- `subbundles/03-calendar-and-generated-asset-split` completed with a passed closure gate

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib`
- `C:\repositories\CanDoItAll\tools\canvaslib\asset-manifest.json`
- `C:\repositories\CanDoItAll\tools\canvaslib\build-assets.cjs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\SharedCanvasBrowserTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj`

## Deliverables

- Final regenerated CanvasLib static assets and include components.
- Final build, test, browser, and line-count evidence recorded in the execution report.
- A QA closure statement that the resulting CanvasLib structure is logical and under the size ceiling.

## Dependency Impact

- This is the final bundle gate.
- If this phase is weak, the user’s explicit completion condition is not met and the task must remain open.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Regenerate assets from the final manifest.
2. Run asset verification, targeted tests, and browser proof on both main routes.
3. Run a line-count audit over the entire CanvasLib package.
4. Review the resulting folder structure as a senior QA / maintainability gate.
5. Update the bundle status only if every closure condition passes.

## Scope Exceptions

- `none`

## Do Not Do

- Do not wave through any remaining file above 2000 lines.
- Do not replace browser proof with reasoning.
- Do not mark the bundle complete while any subbundle gate or raw-note closure row is still pending.

## Acceptance Checklist

- Asset regeneration and verification pass cleanly.
- Targeted test suites pass cleanly.
- Structure and calendar browser proof pass cleanly.
- The final line-count audit reports zero CanvasLib files above 2000 lines.
- The resulting folder structure is coherent and reviewable by responsibility.

## Proof Required

- `npm run canvaslib:build-assets`
- `npm run canvaslib:verify-assets`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj`
- A line-count audit over `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib`
- Browser screenshots and console checks for structure and calendar routes

## Browser Validation Logging

- Routes: `/projects/{projectId}/structure`, `/projects/{projectId}/calendar`
- Viewport: `1600x900`
- Required actions: final smoke after all regeneration and test passes, confirm route health and absence of console/runtime failures
- Screenshot paths: `output/playwright/canvaslib-final-structure.png`, `output/playwright/canvaslib-final-calendar.png`
- Review questions:
  - Is the final CanvasLib asset graph stable on both routes?
  - Is the resulting folder structure obvious enough for future maintainers?
  - Did the final audit find any file above 2000 lines?

## Progression Gate

- The bundle closes only if all proof passes and the final line-count audit reports no file above 2000 lines anywhere under `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib`.

## Suggested Agent Prompt

```text
Implement subbundle 04 only. Regenerate the final CanvasLib assets, run the required tests and browser proof, perform the final line-count audit over the full package, and close the bundle only if every CanvasLib file is at or below 2000 lines.
```
