# 03-database-management-ui

## Status

- `Completed`

## Objective

- Add the database-management transfer modal and new-database transfer prompt using the generic transfer service.

## Covered Inputs

- DB management modal with source DB list.
- New DB creation asks whether to transfer basic settings.
- Checkboxes for ProjectStructure MCP token, AI providers, AI agents, and processes.

## Prerequisites

- `01-01-transfer-foundation` closure gate must pass.
- `02-02-workspace-transfer-handlers` closure gate must pass.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\DatabaseSourcesSettingsPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Database\DatabaseProfileWorkspaceService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayoutDatabaseDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.DatabaseProfiles.cs`

## Deliverables

- Transfer modal in database management.
- Source DB selector.
- Checkbox list driven by transfer descriptors/previews.
- Transfer result feedback.
- New database creation prompt using the same transfer items.

## Dependency Impact

- This is the user-facing acceptance surface. Weak UI proof leaves the raw request unresolved even if handlers work.

## Validation Depth

- Critical UI foundation.

## Implementation Steps

1. Extend workspace DB service to expose transfer source/preview/execute methods.
2. Add modal state and markup to the database sources panel using shared `Dialog`, `Stack`, and `Grid`.
3. Open the transfer modal after creating a new empty DB and preselect the current/source DB where possible.
4. Add equivalent managed-SQLite startup/main-layout creation prompt or route it through the same transfer UI.
5. Keep UI text concise and avoid clear secret values.

## Scope Exceptions

- If browser startup is blocked, record the blocker and keep closure incomplete until visual proof is captured.

## Do Not Do

- Do not hard-code copy behavior in Razor.
- Do not add one-off structural CSS when shared components can express the layout.

## Acceptance Checklist

- User can open a transfer modal for a selected target DB.
- Modal lists eligible source DBs.
- Modal shows checkboxes for the four initial transfer items.
- New DB creation asks about transfer options.
- Transfer action reports item-level results.

## Proof Required

- Large-screen Playwright proof of modal open state.
- Screenshot review for readability, clipping, alignment, and layering.
- Narrower-width check if the route can be reached.

## Browser Validation Logging

- Route: database settings/data sources route or the app route that hosts `DatabaseSourcesSettingsPanel`.
- Viewports: large desktop first; narrower width if route is reachable.
- Actions: open database management, select a DB, open transfer modal, inspect source selector and item checkboxes, toggle one checkbox, close/apply as safe.
- Screenshots: save under bundle evidence if possible and list them in `reviews/01-execution-report.md`.

## Progression Gate

- Proceed only when browser evidence confirms the modal and new-database prompt are usable and complete, or an explicit environment blocker is recorded.

## Suggested Agent Prompt

```text
Implement the database-management transfer UI using shared components and capture browser proof of the open modal and creation prompt.
```
