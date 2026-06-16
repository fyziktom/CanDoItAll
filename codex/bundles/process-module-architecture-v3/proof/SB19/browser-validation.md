# SB19 Browser Validation

## Route And Viewport

- Route: `/processes`, then `/projects/{projectId}/processes?runId=55555555-5555-5555-5555-555555555555`.
- Harness: Playwright through `CanDoItAll.Tests.Playwright`.
- Viewport: 1440x900.

## Actions

1. Opened the global Process shell and selected the Definitions tab.
2. Searched for `architecture`, selected `architecture-decision-governance`, saved/published the definition draft, and exercised dependent canvas/step/role smoke coverage.
3. Opened the template library panel.
4. Searched for `AI-assisted`, selected the `Processes` category, and selected `process:ai-assisted-change-delivery`.
5. Opened Markdown, diagram, JSON, and structure preview tabs.
6. Captured `processes-template-library-preview.png`.
7. Imported the selected process template and asserted an import receipt.
8. Imported the first related role component and asserted a role import receipt.
9. Selected the first artifact target step and imported the first related artifact component.
10. Captured `processes-template-library-imports.png`.
11. Exercised the project-scoped Process route as dependent smoke coverage.

## Assertions

- Template library rendered after shell load.
- Search/category selection resolved the AI-assisted process template.
- Preview text contained `AI-assisted`.
- Markdown, diagram, JSON, and structure tabs rendered their dedicated test IDs.
- Process import receipt contained `imported`.
- Role import receipt contained `Role component`.
- Artifact import receipt contained `Artifact component`.
- No Blazor error UI was visible.
- Browser summary recorded `FailedRequests=0` and `PageErrors=0`; two `/_blazor/disconnect` cleanup posts were recorded as ignored expected Blazor disconnect noise.

## Screenshots

- bundle://proof/SB19/browser/processes-template-library-preview.png
- bundle://proof/SB19/browser/processes-template-library-imports.png
- bundle://proof/SB19/browser/processes-definition-step-editor.png
- bundle://proof/SB19/browser/processes-definition-canvas.png
- bundle://proof/SB19/browser/processes-definition-role-editor.png
- bundle://proof/SB19/browser/processes-global-definition-catalog.png
- bundle://proof/SB19/browser/processes-project-shell.png

## Console And Network Summary

- bundle://proof/SB19/browser/browser-validation-summary.txt

Result: passed through Playwright; no Blazor error UI, page error, unexpected failed request, or assertion failure interrupted the owned flow.
