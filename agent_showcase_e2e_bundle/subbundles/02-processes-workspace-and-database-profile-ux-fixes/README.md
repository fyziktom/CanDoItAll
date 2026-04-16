# Processes workspace and database profile UX fixes

## Status

- `Completed`

## Objective

- Restore basic usability for the Processes workspace and database profiles dialog by fixing scroll containment and adding copy affordances for visible paths.

## Covered Inputs

- `U002`
- `U003`
- Functional requirements `3` and `4`

## Prerequisites

- Prepared bundle validator pass
- No earlier code subbundles

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\ProcessesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayoutDatabaseDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.DatabaseProfiles.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\MainLayoutDatabaseProfileTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProcessManagementBundle.cs`

## Deliverables

- Processes workspace scroll behavior works again on the affected desktop layout.
- Database profiles dialog exposes copy buttons for every visible path surface in scope.
- Regression coverage exists for the changed UI behavior or JS interaction path.

## Dependency Impact

- Subbundles `03` and `04` rely on the Processes workspace being usable during showcase setup and live execution.
- Weak proof here would make later browser validation ambiguous because failures could be caused by the base UI regression rather than by process-runtime logic.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Reproduce or inspect the Processes workspace containment problem and fix it at the smallest layout boundary that restores scrolling.
2. Add copy affordances for the active selection path and each profile path shown in the database dialog, reusing existing JS interop patterns where possible.
3. Add or update tests for the dialog surface and browser proof for the Processes workspace.
4. Capture browser evidence for both the scroll fix and the copy-button visibility.

## Scope Exceptions

- This phase does not run the showcase or provision template-driven data.

## Do Not Do

- Do not introduce brittle CSS overrides that only mask the symptom on one viewport.
- Do not add copy buttons for hidden or unavailable paths that are not actually rendered to the user.
- Do not rely on browser-only proof without at least one targeted automated regression.

## Acceptance Checklist

- `/processes` can scroll vertically where expected on the affected desktop viewport.
- The database dialog visibly exposes copy buttons next to the path surfaces in scope.
- Copy actions are wired through JS interop or equivalent implementation without breaking existing dialog flows.
- Existing database dialog tests still pass or are updated to cover the new affordances.

## Proof Required

- Targeted test command covering the database dialog surface.
- Browser pass on `/processes` and the database dialog at a large desktop viewport.
- Planned screenshots:
  - `C:\repositories\CanDoItAll\agent_showcase_e2e_bundle\reviews\evidence\02-processes-scroll.png`
  - `C:\repositories\CanDoItAll\agent_showcase_e2e_bundle\reviews\evidence\02-database-dialog-copy-buttons.png`
- DOM or assertion proof that the process list or detail area scrolls and that copy controls are rendered.

## Closure Evidence

- Process workspace fix restores internal scroll containment without reintroducing document-level overflow.
- Database dialog now exposes copy controls for the resolved target path, workspace root, and visible profile rows.
- Targeted regression suite passed, including:
  - `CanDoItAll.Tests.Components.ListDetailShellTests.Explicit_min_height_classes_replace_the_default_pane_min_height`
  - `CanDoItAll.Tests.Components.MainLayoutDatabaseProfileTests.Main_layout_database_dialog_renders_copy_buttons_for_visible_database_targets`
  - `CanDoItAll.Tests.Components.ProcessWorkspaceTests.Workspace_shell_uses_internal_scroll_regions_for_definition_list_and_detail_tabs`
- Live browser proof on the requested profile showed:
  - `/processes`: internal scroll container accepted `scrollTop 0 -> 160` while the document stayed pinned to the viewport
  - Database dialog: copy buttons rendered for the active database target, workspace root, and visible profile rows
- Screenshots captured:
  - `C:\repositories\CanDoItAll\agent_showcase_e2e_bundle\reviews\evidence\02-processes-scroll.png`
  - `C:\repositories\CanDoItAll\agent_showcase_e2e_bundle\reviews\evidence\02-database-dialog-copy-buttons.png`

## Browser Validation Logging

- Target route: `/processes`
- Additional surface: runtime database dialog opened from the shell
- Required viewport: `1600x900`
- Narrower follow-up: only if the containment fix materially changes responsive layout
- Required browser actions: navigate to `/processes`, create or select enough content to validate vertical scrolling, open the database dialog, and confirm copy controls are visible.
- Review questions:
  - Is the Processes workspace actually scrollable rather than visually clipped?
  - Are copy buttons present for the rendered paths without crowding or overlap?
  - Does the dialog remain usable after the added controls?

## Progression Gate

- Downstream work may continue only when both affected UI surfaces are browser-proven usable and targeted regression coverage exists for the changed code paths.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Fix the Processes workspace scroll regression at the correct layout boundary and add copy buttons for the visible database-profile paths in the database dialog. Add targeted regression coverage and browser proof before closure.
```
