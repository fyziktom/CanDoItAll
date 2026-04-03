# Phase 04 fresh-db seeding tests and browser proof

## Status

- `Completed`

## Objective

- Prove the dependency feature end-to-end on a fresh SQLite profile with realistic seeded data, automated coverage, and Playwright MCP screenshots.

## Covered Inputs

- `N011`
- `N012`
- `N013`
- `N014`
- `RQ-013`
- `RQ-014`
- `RQ-015`
- `NFR-003`

## Prerequisites

- `subbundles/01-phase-01-models-persistence-and-mcp-dependency-surfaces`
- `subbundles/02-phase-02-canvas-toolbar-modes-and-dependency-authoring-ux`
- `subbundles/03-phase-03-dependency-intelligence-and-mermaid-gantt-export`

## Exact Source References

- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\PlaywrightAppFixture.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureCanvasFeedbackBundle.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs
- C:\repositories\CanDoItAll\project-structure-dependency-execution-bundle\reviews\01-execution-report.md

## Deliverables

- Fresh-SQLite seed or test-fixture flow that exercises multiple node kinds and dependency chains.
- Automated test coverage for the seeded scenario where appropriate.
- Playwright MCP browser proof with screenshots for dependency mode, delete mode, and moved-node link persistence.
- Updated execution report with commands, screenshot findings, and raw-note closure.

## Dependency Impact

- This is the final closure phase; weak proof here means the feature cannot be claimed complete.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Create or seed a fresh managed SQLite profile with realistic dependency graph data, preferably reusing the prepared bundle structure where it helps.
2. Extend automated tests to cover the fresh-data flow and any remaining regression gaps.
3. Run Playwright MCP against the project structure page, create and delete dependencies, move nodes, and verify the visual result with screenshots.
4. Update the execution report with commands, browser analytics, screenshot findings, and raw-note closure.

## Scope Exceptions

- None expected; this phase is responsible for final closure.

## Do Not Do

- Do not validate against the legacy database.
- Do not leave screenshot findings unwritten; image paths alone are insufficient.
- Do not close the bundle if the seeded data is too trivial to exercise notes, tasks, and higher-level structure nodes together.

## Acceptance Checklist

- Fresh-SQLite validation data exists and is clearly not the legacy database.
- Automated tests cover the intended dependency scenario.
- Playwright proof demonstrates dependency creation, deletion, arrowed-link persistence during movement, and delete highlighting.
- Execution report and raw-note closure sections are updated honestly.

## Proof Required

- Targeted integration and component test commands for final regression proof.
- Playwright and browser run on a fresh managed SQLite profile.
- Screenshots showing dependency mode and delete mode.
- Written screenshot findings calling out tool visibility, hover highlight, arrow direction, and link persistence while moving nodes.

## Browser Validation Logging

- Route: `/workbench/projects/{projectId}/structure`
- Viewports: `1600x900` required; add `1280x900` if toolbar wrapping or overlay density changes.
- Required actions: seed data, open the structure canvas, create a dependency between two nodes, drag a connected node, enter delete mode, remove a link, and verify any risky-node delete confirmation path.
- Screenshot targets: `evidence/project-structure-dependency-desktop.png`, `evidence/project-structure-delete-desktop.png`, plus a tablet-width screenshot if layout changes.
- Screenshot review questions: can the user see which tool is active, does the arrow show dependency direction, is hover highlight obvious enough for delete mode, and does the moved-node screenshot prove the curve stayed attached?

## Progression Gate

- Final closure only. Do not mark execution complete until the fresh-SQLite Playwright proof and written screenshot review are logged in `reviews/01-execution-report.md`.

## Suggested Agent Prompt

```text
Implement Phase 04 only.

Use a fresh managed SQLite profile, not the legacy database.
Seed realistic project-structure data, run Playwright proof, capture screenshots, and update the execution report with honest findings.
Do not claim closure without the screenshot review notes.
```
