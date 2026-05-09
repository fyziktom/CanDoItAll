# Project Structure Page Title

## Status

- `Completed`

## Objective

- Render project-structure document titles as `PS - <project name>` with deterministic ellipsis truncation for long names.

## Covered Inputs

- N001 page title requirement.
- R001 page title.

## Prerequisites

- Root bundle prepared-stage validation has passed.
- No prerequisite implementation subbundle is required.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`

## Deliverables

- `PageTitle` uses a helper derived from `surface.ProjectName`.
- Long project names are truncated with `...`.
- Focused test coverage proves normal and long title behavior.

## Dependency Impact

- This phase is independent of agent tooling.
- Weak proof affects only browser/page chrome, not later service/tool subbundles.

## Validation Depth

- UI chrome with component-test proof and optional browser title proof.

## Implementation Steps

1. Add a title helper in `ProjectStructurePage.razor`.
2. Replace the static `PageTitle`.
3. Add or update component tests for title output.
4. Record proof in the execution report.

## Scope Exceptions

- No visual redesign or header copy change is included.

## Do Not Do

- Do not change the visible `PageHeader` titles unless required by tests.
- Do not add CSS-only truncation as the only solution.

## Acceptance Checklist

- `PS - Demo Project` appears for short names.
- A long project name is shortened and ends in `...`.
- Loading/unavailable state has a safe fallback title.

## Proof Required

- Targeted component test for `ProjectStructurePage`.
- Optional browser assertion on `/projects/{projectId}/structure` if app proof is practical.

## Proof Captured

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~ProjectStructurePageTitleBuilderTests|FullyQualifiedName~ProjectStructureNodeCatalogTests"` passed.

## Browser Validation Logging

- Route: `/projects/{projectId}/structure`.
- Viewport: large desktop if browser proof is captured.
- Actions/assertions: navigate, wait for structure page, assert `document.title`.
- Screenshot: optional; title assertion is the primary browser proof.

## Progression Gate

- Title helper and tests pass before marking N001 solved.

## Suggested Agent Prompt

```text
Implement only the project-structure page title change. Keep the visible page layout unchanged, add focused coverage for title truncation, update the execution report, and stop if the page no longer has access to project name state.
```
