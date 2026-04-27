# 03-ui-exposure-and-workflow-proof

## Status

- `Completed`

## Objective

Expose all-project transfer and project zip import/export through the UI, then capture real browser proof.

## Covered Inputs

- `N002`: zip import/export
- `N003`: transfer between existing databases via UI
- `N004`: same transfer model as existing new-database flow

## Prerequisites

- `01-project-database-transfer` completed and trusted.
- `02-project-zip-package-import-export` completed and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\DatabaseSourcesSettingsPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayoutDatabaseDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectsBoard.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectsPageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\MainLayoutDatabaseProfileTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.DatabaseProfiles.cs`

## Deliverables

- Projects page controls to export all projects to zip and import all projects from a package path.
- User-facing message states for success/failure/busy import/export operations.
- Confirmation that existing transfer dialogs show the `Projects` transfer item.
- Component and browser proof for `/projects` and database transfer UI.

## Dependency Impact

- Subbundle `04` depends on the UI proof rows and screenshots captured here to close the user's UI-specific requirements.
- If either route lacks browser proof, final closure must reopen this subbundle.

## Validation Depth

- `UI, component-test, browser-proof`

## Implementation Steps

1. Inject and call the project package service from `ProjectsPage`.
2. Add compact export/import controls in the Projects board/page surface.
3. Keep controls consistent with the existing board toolbar style and avoid oversized explanatory UI.
4. Add component tests for controls, messages, and service calls.
5. Confirm no explicit UI changes are needed for database transfer dialogs beyond handler registration; add tests or browser proof that `Projects` appears.
6. Run Playwright proof against `/projects` and `/settings?tab=data-sources`.

## Scope Exceptions

- Browser-native file picker upload/download is not required if the UI provides local package path export/import consistent with existing snapshot UI patterns.

## Do Not Do

- Do not redesign the Projects board.
- Do not create a landing/explanation page.
- Do not add visible instructional walls of text.

## Acceptance Checklist

- `/projects` exposes all-project zip export and import controls.
- Successful export displays a package path.
- Import requires a package path and displays success/error status.
- Existing transfer UI offers `Projects` alongside processes, agents, providers, and MCP settings.
- Controls fit and remain readable at desktop and narrower widths.

## Proof Required

- Component tests for Projects page controls.
- Browser proof on `/projects` at large desktop viewport and a narrower viewport.
- Browser proof on `/settings?tab=data-sources` or startup transfer prompt showing `Projects` as a transfer option.
- Screenshot review answers recorded in `reviews/01-execution-report.md`.
- Evidence captured under `C:\repositories\CanDoItAll\project_import_export_transfer_bundle\evidence`.

## Browser Validation Logging

- Route: `/projects`
- Route: `/settings?tab=data-sources`
- Viewports: large desktop first, then narrower follow-up.
- Required Playwright actions: navigate, locate controls, trigger previewable states where feasible, capture screenshots.
- Screenshot paths: record under `.artifacts` or bundle `evidence/`.
- Review questions: readability, clipping, overlap, alignment, space use, message visibility, and coherence with existing visual system.

## Progression Gate

- Passed. Browser proof confirms `/projects` exposes zip export/import controls at desktop and narrow widths, and the existing data-source transfer dialog exposes the registered `Projects` transfer item with source and target counts.

## Suggested Agent Prompt

```text
Implement subbundle 03 only: wire project package UI controls and prove both project zip UI and existing database transfer UI in the browser. Do not revisit transfer copy rules unless proof shows a foundation defect.
```
