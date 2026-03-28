# tests browser proof and closure audit

## Status

- `Completed`

## Objective

- Close the initiative with targeted regression coverage, browser analytics, screenshot review, raw-note closure, and final bundle synchronization.

## Covered Inputs

- `N005` prove the final composition uses space more efficiently around the selected root
- `N006` prove there are no canvas collisions
- `N008` finish the full bundle workflow through closure
- `N009` close the screenshot complaint with actual browser evidence

## Prerequisites

- Subbundle `01-subtree-radial-layout-engine-and-persistence-foundation` is completed
- Subbundle `02-toolbar-triggered-selected-subtree-recomposition-workflow` is completed
- No critical proof from earlier phases is still weak or contradicted

## Exact Source References

- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs
- C:\repositories\CanDoItAll\project-structure-node-recomposition-bundle-1\reviews\01-execution-report.md
- C:\repositories\CanDoItAll\project-structure-node-recomposition-bundle-1\README.md

## Deliverables

- Final targeted automated test runs recorded with outcomes
- Browser analytics rows and screenshot artifacts recorded in the execution report
- Raw-note closure updated to `Solved`, `Partially solved`, or `Not solved`
- Final bundle status synchronized with actual shipped proof

## Dependency Impact

- This is the closure phase. Weak proof here would leave the user request unresolved even if code exists.
- Any gap found here must reopen the affected earlier subbundle instead of being hidden in prose.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Run the final targeted test commands for the service and page seams.
2. Run real browser validation on the project structure page and capture large-screen and narrower screenshots.
3. Record browser analytics and gate decisions in `reviews/01-execution-report.md`.
4. Reopen the original raw notes and mark each one `Solved`, `Partially solved`, or `Not solved`.
5. Synchronize subbundle statuses, root validation summary, and the final closure gate.

## Scope Exceptions

- If any raw note remains partial, create a follow-up item instead of claiming closure.

## Do Not Do

- Do not use reasoning-only closure for a layout complaint.
- Do not skip screenshot review after capturing browser artifacts.
- Do not leave any executed subbundle in `Ready` or `In progress`.

## Acceptance Checklist

- Final targeted test commands pass.
- Browser analytics rows are fully populated with real actions and screenshots.
- Raw notes are closed note by note.
- Bundle summary, subbundle statuses, and execution report all match reality.

## Proof Required

- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProjectStructurePageTests"`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests"`
- Browser screenshots at:
  - `output/project-structure-node-recomposition-bundle-1/recompose-desktop.png`
  - `output/project-structure-node-recomposition-bundle-1/recompose-narrow.png`
- Recorded overlap check results from the browser session
- Updated raw-note closure table in `reviews/01-execution-report.md`

## Browser Validation Logging

- Route: `/projects/<projectId>/structure`
- Viewports:
  - `1600x1000`
  - `1280x820`
- Playwright evidence:
  - open the page
  - trigger subtree recomposition from the toolbar
  - evaluate node bounds for overlap
  - capture screenshots
  - confirm the selected subtree stays visible and denser around the root
- Screenshots:
  - `output/project-structure-node-recomposition-bundle-1/recompose-desktop.png`
  - `output/project-structure-node-recomposition-bundle-1/recompose-narrow.png`
- Review questions:
  - can the user see more of the subtree without extra panning?
  - is the composition clearly less one-directional than before?
  - are any nodes colliding, clipped, or awkwardly spaced?

## Progression Gate

- Final closure may pass only when automated tests, browser screenshots, analytics rows, and raw-note closure all show the request is solved.

## Suggested Agent Prompt

```text
Implement subbundle 03 only.
Do not add new feature scope.
Run the targeted final proof, capture browser analytics and screenshots, close each raw note honestly, and synchronize the bundle status with the shipped result.
```
