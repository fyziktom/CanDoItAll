# Definition summary counts and verification closure

## Status

- `Ready`

## Objective

- Make definition summary cards count roles and steps from one authoritative version per definition, then close the bundle with build, MCP, DB, and browser proof.

## Covered Inputs

- Published definitions currently double their role and step counts because every version row is aggregated.
- Closure must prove the browser-visible counts and MCP-backed data are coherent after the repair.

## Prerequisites

- Subbundle 01 closed with component and browser proof.
- A definition exists that has been published so a new draft clone can exercise the count regression.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\codex\bundles\processes-mcp-ui-repair-2026-04-09\reviews\01-execution-report.md`

## Deliverables

- One-version summary-count logic in `ProcessesService.ListDefinitionsAsync`.
- An integration test that fails when publish-plus-cloned-draft doubles role or step counts.
- Final execution report entries covering build, tests, MCP replay, DB inspection, and browser confirmation.

## Dependency Impact

- This is the closure phase. Weak proof here would leave the bundle with a fixed page load but unreliable list summaries and no end-to-end evidence.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Resolve the authoritative summary version selection for each definition in `ListDefinitionsAsync`.
2. Add or extend an integration test to publish a definition and verify non-doubled role and step counts.
3. Rebuild and rerun targeted tests.
4. Re-run MCP listing, confirm active managed-profile data, and verify the visible browser counts.
5. Record the final proof and residual risks in the execution report.

## Scope Exceptions

- Does not redesign how version numbers are presented beyond keeping counts coherent with the authoritative summary version.

## Do Not Do

- Do not change process run creation, publish rules, or version-cloning semantics unless required to keep counts coherent.
- Do not widen the scope into project-structure MCP failures or unrelated workspace issues.

## Acceptance Checklist

- A published definition with a cloned draft no longer shows doubled role counts.
- A published definition with a cloned draft no longer shows doubled step counts.
- MCP listing, DB contents, and browser-visible summaries agree after the fix.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Integration --filter FullyQualifiedName~ProcessesServiceIntegrationTests`
- `dotnet test tests/CanDoItAll.Tests.Components --filter FullyQualifiedName~ProcessWorkspaceTests`
- Targeted build of the affected solution or projects.
- MCP definition list call, managed-profile DB query, browser DOM assertions, and screenshot capture.

## Browser Validation Logging

- Route: `/processes` and `/processes?processId=<smoke-definition-id>`
- Viewports: `1600x900` desktop proof.
- Required actions: navigate, assert non-zero summary tiles, assert the smoke definition summary reflects the correct role and step totals, capture screenshot(s).
- Expected artifacts: `artifacts/processes-mcp-ui-repair/processes-global-desktop.png` and `artifacts/processes-mcp-ui-repair/processes-definition-desktop.png`
- Review questions: do the rendered role and step counts match the authoritative version instead of the total across all versions?

## Progression Gate

- The bundle can close only after targeted tests pass, the browser reflects the corrected counts, and the execution report contains the MCP, DB, and browser evidence.

## Suggested Agent Prompt

```text
Implement only the authoritative summary-count repair and final verification work. Do not expand into token/config features.
```
