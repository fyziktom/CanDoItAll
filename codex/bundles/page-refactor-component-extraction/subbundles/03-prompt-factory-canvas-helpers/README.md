# Prompt Factory Canvas Helpers

## Status

- `Completed`

## Objective

- Extract pure PromptFactory canvas graph, recommendation, and display helper logic before PromptFactory markup is split.

## Covered Inputs

- `N003`
- `R003`

## Prerequisites

- Prepared-stage bundle validator passed.
- Workbook rows for PromptFactory helper candidates are populated.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\CanvasAdapters\PromptFactorySessionGraphAdapter.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\PromptFactoryPageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanvasAdapterTests.cs`

## Deliverables

- PromptFactory page helpers for canvas graph requests, node/link construction, labels, palettes, branch labels, and checkbox parsing are moved to typed helpers where pure.
- Existing adapters are reused or extended instead of creating duplicate graph logic.

## Dependency Impact

- `04-prompt-factory-page-shell-components` depends on stable helper output and canvas state persistence behavior.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Identify pure helpers in the lower `@code` region of `PromptFactoryPage.razor`.
2. Prefer existing canvas adapter classes when logic belongs there.
3. Create page-local helper classes only for page-specific formatting and parsing.
4. Update tests or add helper tests for branch-heavy logic.
5. Run PromptFactory page and canvas adapter tests.

## Scope Exceptions

- Do not move build/save/send service orchestration.
- Do not change session navigation or history behavior.

## Do Not Do

- Do not split PromptFactory markup in this phase.
- Do not change canvas action ids or node ids.

## Acceptance Checklist

- Helper extraction reduces PromptFactory page `@code` size. `Passed`: page-local graph builders were removed and page-specific helpers moved to `PromptFactoryPageHelpers`.
- Canvas graph output remains stable. `Passed`: existing `PromptFactorySessionGraphAdapter` remains the canvas node/link owner.
- PromptFactory component tests pass. `Passed`.

## Implementation Summary

- Added `PromptFactoryPageHelpers` for typed canvas graph request construction, recommendation overlay fallback calculation, optional GUID parsing, prompt node-id parsing, branch labels, and checkbox value parsing.
- Removed duplicate page-local canvas node/link builders from `PromptFactoryPage.razor` and `PromptFactoryPage.Catalog.cs`; live canvas construction remains in `PromptFactorySessionGraphAdapter`.
- Preserved canvas node ids, action ids, session navigation, and history/state orchestration.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter PromptFactoryPage --logger "console;verbosity=minimal"`: `Passed`, 4 tests.
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter CanvasAdapterTests --logger "console;verbosity=minimal"`: `Passed`, 4 tests.
- PromptFactory browser smoke: `Not required` for this helper-only phase because node ids, action ids, visible labels, and layout construction remain owned by the existing adapter.

## Browser Validation Logging

- Route: `/prompt-factory`.
- Viewport: `1600x900`.
- Required actions: navigate and confirm canvas/session surface still renders if helper output is browser-visible.
- Screenshots: required only if canvas output changes.

## Progression Gate

- `Passed`: PromptFactory tests pass and canvas helper output is stable. `04` and `08` may proceed.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Extract pure PromptFactory canvas helpers, reuse existing adapters where appropriate, preserve canvas ids and actions, run targeted tests, and update the execution report.
```
