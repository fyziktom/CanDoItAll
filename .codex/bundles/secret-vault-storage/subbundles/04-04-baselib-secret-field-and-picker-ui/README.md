# BaseLib Secret Field And Picker UI

## Status

- `Completed`

## Objective

- Add reusable BaseLib secret reveal/copy behavior and use it in settings and project-structure secret picker/create flows.

## Covered Inputs

- `N011`, `N012`
- `R010`, `R011`

## Prerequisites

- `SB02` closure gate passed.
- `SB03` reference models are available or the UI stores reference-only metadata through existing models.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\Password.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Buttons\CopyButton.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Inputs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureSupportDialogs.razor`

## Deliverables

- BaseLib component for secret values with copy name/value affordances and `Show for 30s` auto-hide.
- Settings secret editor uses the BaseLib component.
- Secret list/detail panel exposes copy button for secret name and controlled copy/reveal for the value.
- Project-structure secret dialog supports search, select existing secret, and create new secret.
- Sandbox example documents the BaseLib component behavior.

## Dependency Impact

- `SB05` final browser proof depends on this UI being readable and safe.

## Validation Depth

- `UI and component validation`

## Implementation Steps

1. Add or enhance BaseLib component using `CopyButton`, `Button`, `Stack`, and existing field CSS patterns.
2. Add timed reveal state with cancellation/disposal cleanup.
3. Replace settings raw password input with the shared component.
4. Add project-structure picker/create dialog using existing dialog primitives.
5. Add sandbox usage and component tests where existing patterns support it.

## Scope Exceptions

- If project-structure local composition makes the full dialog too large for this pass, add the shared picker component and wire the existing "add secret" action to it with a documented follow-up for deeper canvas polish.

## Do Not Do

- Do not create a one-off password/copy control on product pages.
- Do not leave secret text visible after the timeout.
- Do not put raw secret values into project-structure node metadata.

## Acceptance Checklist

- [x] `Show for 30s` reveals and auto-hides.
- [x] Copy secret name and value buttons are available where requested.
- [x] Project-structure secret dialog can search and create/select a secret.
- [x] UI uses BaseLib components instead of raw custom wrappers where possible.

## Proof Captured

- `dotnet build src\CanDoItAll.Components.Sandbox\CanDoItAll.Components.Sandbox.csproj`: passed.
- Browser proof for `/settings?tab=secrets`: `SecretField` hidden by default, reveal state, copy controls, and auto-hide after 32 seconds verified.
- Browser proof for `/projects/d8fc823b-beef-4aac-b163-4a6d4d7ff010/structure`: Quick Create > Assets > Secret opens the dedicated add-secret-reference dialog with search/create/copy/reveal controls.
- Screenshots:
  - `C:\repositories\CanDoItAll\secret-settings-revealed.png`
  - `C:\repositories\CanDoItAll\project-structure-secret-dialog.png`

## Proof Required

- `dotnet build src\CanDoItAll.Components.BaseLib\CanDoItAll.Components.BaseLib.csproj`
- Component tests if available for the changed UI surface.
- Browser screenshots for `/settings?tab=secrets` and project-structure secret dialog.

## Browser Validation Logging

- Routes: `/settings?tab=secrets`, `/project-structure`
- Viewport: `1600x900` and narrower width if layout changes.
- Evidence: open secret editor with value hidden, reveal, wait/verify hide, copy controls visible, project-structure dialog open with search/create controls.

## Progression Gate

- Passed. UI proof shows the dialog and settings editor are usable, copy controls are visible, and timed reveal hides the value.

## Suggested Agent Prompt

```text
Implement SB04 only. Build the shared BaseLib timed secret field, wire settings and project-structure picker UI, capture screenshots, and update browser analytics.
```
