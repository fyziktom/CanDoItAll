# Workspace Settings Helper Extraction

## Status

- `Ready`

## Objective

- Extract helper logic from large workspace settings panels while preserving database source, provider, connector, and storage settings behavior.

## Covered Inputs

- `N001`
- `N003`
- `R006`

## Prerequisites

- Prepared-stage bundle validator passed.
- Workbook rows for settings panels are populated.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\DatabaseSourcesSettingsPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\StorageSettingsPanel.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\SettingsPageDataSourcesTests.cs`

## Deliverables

- Helper classes for database source display/validation helpers where pure.
- Helper classes for storage setting labels, status, and field mapping where pure.
- No change to settings persistence contracts.

## Dependency Impact

- Settings browser proof and final build depend on this phase because settings panels are high-line-count page-owned components.

## Validation Depth

- Component-test and settings route validation.

## Implementation Steps

1. Extract pure helpers from `DatabaseSourcesSettingsPanel`.
2. Extract pure helpers from `StorageSettingsPanel`.
3. Keep persistence calls and edit-state mutation in components.
4. Run targeted settings component tests.
5. Browser smoke `/settings` if rendered settings regions change.

## Scope Exceptions

- Do not redesign settings layout in this phase.

## Do Not Do

- Do not change database profile selection behavior.
- Do not change provider or connector persistence semantics.

## Acceptance Checklist

- Large settings panels lose pure helper logic.
- Existing settings tests pass.
- No settings route regression is visible.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter SettingsPageDataSourcesTests`
- Browser route smoke on `/settings` if visible output changes.

## Browser Validation Logging

- Route: `/settings`.
- Viewport: `1600x900`.
- Required actions: navigate, open database/storage settings regions, screenshot if visible regions changed.

## Progression Gate

- Settings tests pass before final regression closure.

## Suggested Agent Prompt

```text
Implement subbundle 07 only. Extract pure settings panel helpers, preserve persistence and database profile behavior, run targeted settings tests, and update proof rows.
```
