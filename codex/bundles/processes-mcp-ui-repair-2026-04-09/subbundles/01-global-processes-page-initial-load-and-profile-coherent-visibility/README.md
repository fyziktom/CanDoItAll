# Global processes page initial load and profile-coherent visibility

## Status

- `Ready`

## Objective

- Ensure the global processes workspace loads persisted definitions on the first visit to `/processes`, even when `ProjectId`, `processId`, and `runId` are all absent.

## Covered Inputs

- Initial global page visit shows `Definitions 0` even though the active managed profile already contains definitions.
- The repair must keep the existing managed profile behavior and avoid token/config work for Processes MCP.

## Prerequisites

- Bundle prepared and validator green.
- Existing smoke definition or equivalent persisted definition available for browser proof.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs`

## Deliverables

- A first-render-safe parameter loading guard in `ProcessWorkspace`.
- A component test that fails if the workspace stays empty on its initial null-parameter render.
- Browser proof that `/processes` loads the active profile definitions without query parameters.

## Dependency Impact

- The summary-count verification depends on this phase because the UI must load real data before any visible counts can be trusted.
- Weak proof here would let a stale empty-state regression survive while later checks falsely validate only query-string routes.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Patch the parameter-load guard so the first render always performs one workspace load.
2. Add a focused component test that renders `ProcessWorkspace` without parameters after seeding a definition.
3. Start the local web app and verify `/processes` shows persisted definitions on first navigation.

## Scope Exceptions

- Does not change counting behavior across definition versions. That is owned by subbundle 02.

## Do Not Do

- Do not add token, base URL, or core-service configuration for Processes MCP.
- Do not change process runtime or publish behavior.
- Do not rely on query-string navigation as the only proof path.

## Acceptance Checklist

- The component renders at least one persisted definition name on the initial null-parameter load.
- The global browser route `/processes` does not remain in the empty state when persisted definitions exist.
- The repair is limited to workspace loading behavior.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Components --filter FullyQualifiedName~ProcessWorkspaceTests`
- Browser navigation to `/processes` at a desktop viewport with DOM assertions that a known definition is visible.
- Screenshot artifact captured during the browser pass.

## Browser Validation Logging

- Route: `/processes`
- Viewports: `1600x900` desktop proof, then a narrow follow-up only if layout visibly regresses.
- Required actions: navigate, wait for the process workspace shell, assert a persisted definition name is visible, capture a screenshot.
- Expected artifacts: `artifacts/processes-mcp-ui-repair/processes-global-desktop.png`
- Review questions: does the first page load show persisted definitions and non-zero summary tiles without query parameters?

## Progression Gate

- Downstream work may continue only after the component test passes and the browser shows persisted definitions on `/processes` without a `processId` query parameter.

## Suggested Agent Prompt

```text
Implement only the first-render workspace-load repair, add the component proof, and stop before touching version-count logic.
```
