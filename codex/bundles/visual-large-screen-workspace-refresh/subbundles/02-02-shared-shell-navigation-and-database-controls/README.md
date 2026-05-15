# 02-shared-shell-navigation-and-database-controls

## Status

- `Completed`

## Objective

- Refactor the shared app shell into the Economy-style large-screen foundation: collapsed navigation by default, concise menu labels, right-side tooltips, bottom Settings and database actions, no topbar database switch, and maximum workspace width.

## Covered Inputs

- RN-001 improve visual look, working space, and clarity.
- RN-003 use collapsed Economy Simulator menu concept.
- RN-004 reduce menu text and move information to tooltips.
- RN-005 move Settings and Switch DB to bottom-left menu and remove from top page.
- RN-006 show DB info flyout with copy button on hover.
- RN-007 use maximum available page width.
- RN-009 no own CSS; use Tailwind/BaseLib/component options.

## Prerequisites

- SB00-02 desktop shell/overlay primitives passed.
- SB01 route baseline/proposal gate passed.
- Current database switching behavior is understood from existing `MainLayout` and `MainLayoutDatabaseDialog` code.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components\AppShell.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components\AppShellMode.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayoutTopBar.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayoutDatabaseDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.DatabaseProfiles.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Composition\ShellNavigation.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\TooltipTarget.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Typography\CopyableMonoValue.razor`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\evidence\design-proposals\pages\01-shell-baselib-corrected-proposal.png`
- `C:\repositories\CanDoItAll\Tailwind\navigation\workbench-shell.css`
- `C:\repositories\CanDoItAll\Tailwind\surfaces\overlays.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\DatabaseSwitchWorkbenchPlaywrightTests.cs`

## Deliverables

- Collapsed large-screen shell navigation by default with explicit expanded state.
- Shell uses the SB00-02 reusable rail/flyout primitives or documents why the smallest safe implementation must stay in `AppShell`.
- Minimal visible nav labels/icons and tooltip content sourced from typed navigation metadata.
- Bottom shell action area containing Settings and Switch DB.
- Database flyout that opens on hover/focus, shows active DB summary, masks sensitive values, and offers copy for safe DB info.
- `MainLayoutTopBar` no longer renders the active DB block or `Switch database` button.
- Full-width shell/body layout adjustments using shared component/Tailwind mechanisms.

## Dependency Impact

- All downstream page screenshots depend on this shell. If shell width, navigation, or topbar state is wrong, page-level density proof is invalid.
- DB flyout behavior affects settings, database profile tests, and topbar regression coverage.

## Validation Depth

- Critical UI foundation with component, Playwright, and screenshot proof.

## Implementation Steps

1. Add typed shell state/options for collapsed/expanded navigation and bottom utility actions.
2. Split `ShellNavigationItem` display label from tooltip/help text without losing badges or route matching.
3. Update `AppShell` to render a compact large-screen rail by default and an expanded rail state when toggled.
4. Wrap collapsed nav items in `TooltipTarget` with right-side placement and stable test ids.
5. Add bottom utility actions for Settings and database switching.
6. Move database status/flyout behavior into shell utility area and remove DB switch rendering from `MainLayoutTopBar`.
7. Implement safe copy for non-secret database summary text using existing copy primitives or explicit JS interop with masked content.
8. Adjust shared Tailwind/BaseLib shell classes only where component parameters cannot express the layout.
9. Add or update tests for route matching, collapsed labels/tooltips, and database control behavior.

## Scope Exceptions

- Do not remove the existing database management dialog; it remains the deep management flow.
- Do not redesign every page in this subbundle.
- Do not tune mobile or tablet navigation.

## Do Not Do

- Do not add a second shell implementation.
- Do not copy Economy CSS into this repo.
- Do not expose raw connection strings, secrets, or provider credentials in the flyout or copied text.
- Do not add new page-local CSS.

## Acceptance Checklist

- Large-screen shell starts collapsed.
- Navigation can expand and collapse without route loss.
- Collapsed items show readable right-side tooltips.
- Settings and Switch DB are visible at the bottom of the left menu.
- Topbar no longer includes the database switcher or active database details.
- Database flyout opens on hover/focus, shows current profile state, and copy works with masked safe text.
- Main workspace uses more width than before on representative routes.

## Proof Required

- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter Database`
- Targeted Playwright database/shell tests, or updated equivalent if test names change.
- Large-screen screenshots for collapsed rail, expanded rail, nav tooltip, DB flyout, topbar without DB switch, `/projects`, `/processes`, and `/settings`.
- Diff review confirming no new page-local custom CSS.

## Browser Validation Logging

- Routes: `/`, `/projects`, `/processes`, `/settings`.
- Viewport: large desktop, recommended `1920x1080`.
- Actions: load route, inspect collapsed default, hover/focus several nav items, expand rail, hover DB action, click/copy DB summary, open Settings, open DB dialog.
- Screenshots: collapsed rail, expanded rail, right-side tooltip, DB flyout, topbar without DB switch.
- Review questions: does the shell save horizontal space, is tooltip text readable, is the flyout clipped, is DB info safe and useful, and does the main page gain visible workspace.

## Progression Gate

- Page-level subbundles may start only after shell screenshots prove collapsed navigation, bottom DB/Settings controls, no topbar DB switch, and no overlay clipping.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Refactor the existing shared shell rather than creating a parallel shell, move Settings and database controls to the bottom left menu, remove the topbar database switch, use BaseLib/Tailwind mechanisms only, add tests, capture the required large-screen shell screenshots, update the execution report, and stop if the DB flyout cannot be made safe.
```
