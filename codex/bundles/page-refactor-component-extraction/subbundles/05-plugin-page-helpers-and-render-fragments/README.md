# Plugin Page Helpers And Render Fragments

## Status

- `Completed`

## Objective

- Reduce `PluginsPage.razor` helper density by isolating busy keys, tones, test ids, connection editor state, and reusable render fragments without changing plugin install, settings, OAuth, or log behavior.

## Covered Inputs

- `N001`
- `N003`
- `R004`

## Prerequisites

- Prepared-stage bundle validator passed.
- Workbook rows for `/plugins` are populated.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Pages\PluginsPage.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\PluginsPageTests.cs`

## Deliverables

- Helper extraction for busy keys, tone resolution, field placeholders, input types, test id construction, and icon labels.
- Connection editor state moved out of the page if it remains page-local but testable.
- Render fragments extracted only when they can become focused components or typed helpers without hiding UI state.

## Dependency Impact

- Final plugin regression proof depends on stable test ids, OAuth behavior, and connection save behavior.

## Validation Depth

- Critical page helper foundation.

## Implementation Steps

1. Identify pure plugin helper methods and nested classes.
2. Extract strongly typed helper classes or records.
3. Keep service operations and busy-state mutation in the page.
4. Preserve existing `data-testid` output.
5. Run plugin component tests.

## Scope Exceptions

- Do not redesign the plugins page.
- Do not change package install or OAuth security behavior.

## Do Not Do

- Do not weaken plugin trust or manifest validation.
- Do not change secret or OAuth masking behavior.

## Acceptance Checklist

- `PluginsPage.razor` has fewer pure helper methods. `Passed`: page helper/render-fragment logic moved to `PluginsPageHelpers` and connection editor state moved to `PluginConnectionEditorState`.
- Existing plugin component tests pass. `Passed`.
- Test ids remain stable. `Passed`: test id construction moved unchanged and existing component tests passed.

## Implementation Summary

- Added `PluginsPageHelpers` for busy keys, OAuth keys, connection save keys, tone resolution, settings placeholders, input types, test ids, icon labels, icon render fragment, and log render fragment.
- Added `PluginConnectionEditorState` and replaced the nested page state class.
- Left service operations, busy-state mutation, package upload/install, OAuth start/disconnect, and grant updates in `PluginsPage.razor`.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter PluginsPageTests --logger "console;verbosity=minimal"`: `Passed`, 4 tests.
- Browser smoke on `/plugins`: `Not required` for this helper-only phase because the render fragment bodies and test ids were preserved and no visible component split was introduced.

## Browser Validation Logging

- Route: `/plugins`.
- Viewport: `1600x900`.
- Required actions: navigate, select plugin, inspect connection settings, open OAuth action where seeded.
- Screenshots: required if visible components are extracted.

## Progression Gate

- `Passed`: Plugin component tests pass and test-id output remains stable before final closure.

## Suggested Agent Prompt

```text
Implement subbundle 05 only. Extract PluginsPage helpers carefully, preserve security-sensitive OAuth and settings behavior, run PluginsPageTests, and update proof rows.
```
