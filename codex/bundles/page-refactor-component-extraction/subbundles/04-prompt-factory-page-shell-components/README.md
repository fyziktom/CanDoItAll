# Prompt Factory Page Shell Components

## Status

- `Ready`

## Objective

- Split `PromptFactoryPage.razor` into focused shell components after canvas helpers are stable.

## Covered Inputs

- `N001`
- `N002`
- `R008`

## Prerequisites

- `03-prompt-factory-canvas-helpers` completed with passing tests.
- Components MCP retried, or local component guidance fallback recorded.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\Components\PromptFactoryDialogs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\Components\PromptFactorySupportLaneTabs.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\PromptFactoryPageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\PromptFactoryBrowserTests.cs`

## Deliverables

- Extract major PromptFactory markup regions into typed components.
- Keep build/save/export/send/session behavior in the page unless a child component has a clear local state boundary.
- Preserve existing recommendation overlay and dialog behavior.

## Dependency Impact

- Final regression proof depends on this phase for `/prompt-factory`.

## Validation Depth

- UI, component-test, and browser-proof.

## Implementation Steps

1. Identify one coherent visual region in the page shell.
2. Extract the component with typed parameters and callbacks.
3. Preserve state transitions and message/error display behavior.
4. Repeat only for regions listed in the workbook rows for this subbundle.
5. Run component and browser proof.

## Scope Exceptions

- Do not alter prompt generation semantics or provider behavior.

## Do Not Do

- Do not change prompt session persistence.
- Do not create new visual design patterns.

## Acceptance Checklist

- PromptFactory page markup shrinks without changing route behavior.
- Build, save, export, send, branch, and canvas selection callbacks remain stable.
- Dialogs and overlays are readable and layered correctly.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter PromptFactoryPage`
- Relevant PromptFactory Playwright tests or route smoke.
- Screenshots at large and narrow widths for changed regions.

## Browser Validation Logging

- Route: `/prompt-factory`.
- Viewports: `1600x900` and `390x844` when layout changes.
- Required actions: navigate, use the canvas/session region, open dialogs or overlays affected by the split, screenshot.
- Review questions: no clipped overlay, no broken tabs, no missing action affordances.

## Progression Gate

- PromptFactory route proof passes before final regression closure.

## Suggested Agent Prompt

```text
Implement subbundle 04 only. Split PromptFactory page shell regions after confirming subbundle 03, preserve all prompt and canvas callbacks, run targeted tests and browser proof, and update gate rows.
```
