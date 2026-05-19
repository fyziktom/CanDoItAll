# 03-agent-catalog-and-ui-proof

## Status

- Status: `Completed`

## Objective

- Update agent-facing project-structure tool guidance and complete browser proof for the runtime, folder/file, repository, and link workflows.

## Success Criteria

- `project_structure_node_catalog` guidance explains how to add links, runtime scripts, Python runtime nodes, Docker runtime nodes, folders, and files.
- Catalog aliases help agents choose correct objectType/objectSubtype pairs for runtime script, folder, file, link, GitHub, and GitLab requests.
- Playwright MCP screenshots prove the relevant UI surfaces are visible and usable.

## Covered Inputs

- `N005`: agent tools need information about adding links, runtime scripts, folders, and file types.
- `N006`: validate with Playwright MCP and screenshots.
- `R006`
- `R007`

## Prerequisites

- `01-01-runtime-launch-foundation` completed or explicitly blocked with follow-up.
- `02-02-folder-file-link-actions` completed or explicitly blocked with follow-up.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureCanvasCatalog.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureCanvasCatalog.RichDefinitions.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureNodeKindRequestJsonConverters.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Tools\MafAgentRuntime.ProjectStructureTools.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\ProjectStructureNodeCatalogTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\ProjectStructureComposerDefaultsTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs

## Deliverables

- Catalog guidance has concrete metadata keys and examples for runtime scripts, Python, Docker, folders, files, links, GitHub, and GitLab.
- Alias parsing accepts useful typed names without encouraging invented enum names.
- Playwright MCP captures large-screen screenshots and any needed narrower pass.
- Execution report, raw-note closure, and bundle status are synchronized.

## Dependency Impact

- This is the final handoff phase; weak proof here blocks final closure and must reopen whichever foundation was contradicted.

## Validation Depth

- End-to-end closure with unit/component tests, Playwright MCP proof, screenshot review, and completed bundle validator.

## Implementation Steps

1. Update catalog guidance and aliases once supported node metadata is stable.
2. Add unit tests for catalog guidance and alias coverage.
3. Run targeted tests and build checks as needed.
4. Use Playwright MCP to validate the project-structure route and capture screenshots.
5. Update execution report rows, raw-note closure, README status, and run completed-stage validation.

## Scope Exceptions

- If host-level admin PowerShell or Explorer window capture cannot be safely automated, document the exact validation gap and supporting resolver/test proof.

## Do Not Do

- Do not claim UI completion without Playwright MCP screenshots.
- Do not leave raw notes as pending at final closure.
- Do not advertise unsupported node schemas in the agent catalog.

## Acceptance Checklist

- [x] Catalog guidance explicitly names object types, subtypes, and metadata keys for links, runtime scripts, folders, and files.
- [x] Tests pass for catalog guidance and aliases.
- [x] Playwright screenshots show runtime and folder/file/link action surfaces.
- [x] Execution report has non-pending rows for gates, browser analytics, and raw-note closure.
- [x] Completed-stage bundle validator passes.

## Proof Required

- Targeted unit/component tests.
- Playwright MCP screenshots and action/assertion log.
- Final `validate_bundle.py --stage completed` pass.
- Updated `reviews/01-execution-report.md`.

## Browser Validation Logging

- Route: `/workbench/projects/{projectId}/structure`.
- Viewports: large desktop first; narrower pass if dialogs or floating windows changed.
- Actions: create/select runtime script, Python runtime, Docker node, local folder, local file, GitHub/GitLab repository or link; open action surfaces; screenshot each meaningful state.
- Screenshots: save under bundle evidence or existing artifacts path and list in execution report.
- Review questions: action labels readable, dialogs fit, no clipping/overlap, folder/file/link/runtime controls visible, floating windows layer correctly.

## Progression Gate

- Passed. Tests, Playwright MCP proof, raw-note closure, and completed-stage validation all agree.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
