# 02 Components Catalog Split And Tests

## Status

- Status: `Completed`

## Objective

Split the oversized component catalog service around a clear static metadata boundary while preserving catalog search, examples, CSS notes, guidance, and tool behavior.

## Covered Inputs

- N002 detailed refactoring.
- N003 preserve all functions.
- N005 split too long files.
- N006 better testability.

## Prerequisites

- Subbundle 01 is completed and trusted.
- Component catalog tests are identified and runnable.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Catalog\ComponentCatalogService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Catalog\ComponentCatalogModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Tools\ComponentsTools.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Components.Tests\ComponentCatalogServiceTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Components.Tests\ComponentsToolsTests.cs

## Deliverables

- A smaller primary `ComponentCatalogService.cs`.
- A separate catalog metadata partial/helper file for static CSS notes, guidance, tags, summaries, or parameter descriptions.
- Preserved public catalog API behavior and targeted test coverage.

## Dependency Impact

- Final closure depends on this subbundle because it proves the bundle actually split one large MCP implementation file.
- This subbundle does not unlock subbundle 03.

## Validation Depth

- Run component catalog tests.
- Build `CanDoItAll.Mcp.Components`.
- Inspect that moved static metadata remains equivalent and no public catalog methods were removed.

## Implementation Steps

- Mark `ComponentCatalogService` partial if using a partial split.
- Move static metadata fields into a separate file such as `ComponentCatalogService.Metadata.cs`.
- Keep service methods and public API signatures unchanged.
- Add or update a targeted test only if existing tests do not cover the moved metadata.
- Run component tests and focused build.

## Do Not Do

- Do not rewrite catalog search behavior while splitting metadata.
- Do not rename component IDs, groups, route keys, or CSS source paths.
- Do not move metadata into a format requiring runtime file IO unless a test proves equivalence.

## Acceptance Checklist

- `ComponentCatalogService.cs` is shorter and easier to navigate.
- Static catalog metadata has a clear owner file.
- Component catalog tests pass.
- Public catalog method signatures remain unchanged.

## Proof Required

- `dotnet test tests\CanDoItAll.Mcp.Components.Tests\CanDoItAll.Mcp.Components.Tests.csproj`
- Focused build for `src\CanDoItAll.Mcp.Components\CanDoItAll.Mcp.Components.csproj`
- Execution report updated with command outcomes and closure gate decision.

## Browser Validation Logging

- N/A. This subbundle changes server-side MCP catalog code only.

## Progression Gate

- Continue to final closure only after component tests/build pass and no catalog response coverage regresses.

## Suggested Agent Prompt

Implement subbundle 02 after subbundle 01 is closed. Split component catalog metadata from behavior, preserve all public catalog functions, run component tests/build, and update the execution report.
