# Run hierarchy regression proof across tests and browser validation

## Status

- `Completed`

## Objective

- Close the feature as a whole by running the targeted build/test matrix, the cross-surface Playwright proof, the screenshot review, and the raw-note closure audit that confirms the shipped behavior actually matches the user's request.

## Covered Inputs

- `R014`
- `R015`
- Raw notes `N005` through `N013` as final closure targets

## Prerequisites

- `02-add-projects-page-hierarchy-discovery-and-modal-navigation` completed and trusted.
- `03-extend-structure-canvas-for-project-hierarchy-visualization-and-actions` completed and trusted.
- Browser analytics rows for subbundles 02 and 03 have been updated with fresh proof.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectsPageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureGraphAdapterTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectsServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\project-hierarchy-bundle-1\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\project-hierarchy-bundle-1\traceability\02-input-coverage-matrix.md`

## Deliverables

- A clean targeted build/test proof set for the shipped feature.
- Final Playwright proof on `/projects` and `/projects/{id}/structure`.
- Screenshot review that answers the visual questions instead of merely attaching files.
- Raw-note closure updates for the shipped feature notes.
- Reopened earlier subbundles if proof exposes a weak foundation.

## Dependency Impact

- This phase decides whether the feature is honestly complete. If the proof is weak, the bundle must reopen rather than drift into summary prose.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Run the targeted build and automated tests that cover the feature surface.
2. Prepare or seed the browser data required to exercise parent, child, and multi-parent paths.
3. Run real headed Playwright validation on `/projects` and `/projects/{id}/structure`.
4. Review the screenshots against readability, layering, clipping, spacing, and hierarchy clarity.
5. Update browser analytics rows, subbundle gate rows, and raw-note closure rows while the proof is fresh.
6. Reopen any earlier subbundle immediately if the closure proof exposes a defect.

## Scope Exceptions

- This phase does not introduce new feature scope. It closes the implemented scope honestly and reopens earlier phases when required.

## Do Not Do

- Do not treat component or integration tests as a substitute for browser proof.
- Do not mark a raw note solved if its visible behavior was not actually exercised.
- Do not bury missing proof in a residual-risk paragraph.

## Acceptance Checklist

- The targeted build and automated tests pass.
- `/projects` hierarchy discovery flow is proven in a real browser.
- `/projects/{id}/structure` hierarchy nodes and actions are proven in a real browser.
- Screenshot review finds no unresolved clipping, overlap, or hierarchy-clarity defect.
- Raw notes `N001` through `N013` are no longer pending and are mapped to real proof.

## Proof Required

- `dotnet build CanDoItAll.slnx`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProjectsPageTests|FullyQualifiedName~ProjectStructurePageTests|FullyQualifiedName~ProjectStructureGraphAdapterTests"`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectsServiceIntegrationTests|FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests"`
- Headed Playwright MCP proof on both routes
- Screenshot review notes recorded in the execution report

## Browser Validation Logging

- Routes:
- `http://127.0.0.1:5188/projects`
- `http://127.0.0.1:5188/projects/{id}/structure`
- Viewports: `1600x1000`, `1280x900`
- Required Playwright MCP actions:
- prove the Projects page hierarchy filter and recursive modal flow
- prove the structure canvas hierarchy nodes, subdued extra-parent nodes, and new-tab action
- verify the feature works end to end for at least one multi-parent child path
- Required screenshots:
- `C:\repositories\CanDoItAll\output\playwright\project-hierarchy\subbundle-04-projects-desktop.png`
- `C:\repositories\CanDoItAll\output\playwright\project-hierarchy\subbundle-04-structure-desktop.png`
- `C:\repositories\CanDoItAll\output\playwright\project-hierarchy\subbundle-04-projects-narrow.png`
- `C:\repositories\CanDoItAll\output\playwright\project-hierarchy\subbundle-04-structure-narrow.png`

## Progression Gate

- The feature notes are closed with real proof.
- No earlier critical foundation remains contradicted by later proof.
- The execution report is ready for the analytics subbundle to review.

## Suggested Agent Prompt

```text
Implement subbundle 04 only. Treat this as the feature-closure proof phase: run the targeted build/tests, perform real Playwright validation on both hierarchy routes, inspect the screenshots, update the execution report and raw-note closure rows, and reopen any earlier subbundle immediately if the proof exposes a defect.
```
