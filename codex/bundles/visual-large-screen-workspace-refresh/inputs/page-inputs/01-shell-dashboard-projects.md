# Page Inputs: Shell, Dashboard, Projects

## PI-SHELL App Shell And Database Controls

Source references:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components\AppShell.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayoutTopBar.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayoutDatabaseDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Composition\ShellNavigation.cs`

Current display:
- `AppShell` renders a permanent dark left sidebar with app title, nav item title and description text, workbench group summaries, topbar slot, right rail slot, and content body.
- The shell has `StandardPage` and `FocusWorkbench` modes; focus mode is narrower but still not the requested default collapsed global rail.
- `MainLayoutTopBar` currently shows active database details and a `Switch database` button near the top of the page.
- `MainLayoutDatabaseDialog` is the real database switching and management dialog and must remain reachable.

Current UX flows:
- User navigates with visible sidebar routes from `ShellNavigation`.
- User sees active DB at the top and opens DB dialog from the topbar.
- Workbench tabs and recent sessions are opened from shell state through `WorkbenchStateService`.

Target proposal:
- Use `01-shell-baselib-corrected-proposal.png` panels 1-5.
- Default large-screen shell is collapsed and icon-first.
- Expanded rail keeps minimal labels only; long descriptions move to right-side `TooltipTarget` content.
- Settings and Switch DB move to bottom-left rail utilities.
- Hover/focus on DB utility opens a floating card with masked active DB summary, recent DB hints, copy-safe summary, and link to the existing DB dialog.
- Topbar must not show DB selector or active DB chip.

Function coverage confirmation:
- Covers route selection, rail expand/collapse, tooltips, settings navigation, DB dialog launch, active DB display, safe copy, and workspace width.
- Regenerated because the first shell proposal left DB in the topbar; accepted proposal removes topbar DB state.

## PI-DASH Dashboard `/` and `/dashboard`

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Pages\Home.razor`

Current display:
- `PageScaffold` dashboard with summary tiles for `Projects`, `Open work items`, `Validation runs`, and `Failed jobs`.
- Quick actions include `New project` and `Open projects`.
- Empty states cover immediate blockers and open work items.
- Content is currently more card-like than the Economy reference and leaves room for a denser operational landing surface.

Current UX flows:
- User opens dashboard, scans counts, creates a new project, opens projects, and reviews open/recent work.
- Dashboard listens to workbench state for open and recent tabs.

Target proposal:
- Use `05-core-pages-tabs-dialogs-proposal.png` panel 1.
- Replace broad card feel with compact metric strip, operational queues, recent project/activity rows, and concise action toolbar.
- Keep quick actions visible; move explanatory copy to tooltip/help only if needed.

Function coverage confirmation:
- Covers all existing summary counts, new/open project actions, operational queue and recent work scanning.
- Adds visually stronger B2B density without inventing a marketing hero.

## PI-PROJECTS Projects `/projects`

Source references:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectsBoard.razor`

Current display:
- `ProjectsPage` uses `PageScaffold` and delegates the main workspace to `ProjectsBoard`.
- Current filters include search, status, related-party category/value, hierarchy filter project id/mode.
- Actions include new project, preview, hierarchy modal, dashboard, processes, structure, calendar, Gantt preview, export project package, import project package.
- Main display is board/card-first with modal flows.

Current UX flows:
- User filters projects, opens preview modal, creates/edits through project modal, opens structure/processes/calendar routes, previews Gantt, imports/exports package.
- Route query `projectId` can open a modal state.

Target proposal:
- Use `02-project-pages-tabs-dialogs-proposal.png` panel 1.
- Add a large-screen tree/table/detail workspace: searchable project tree grouped by hierarchy/status/domain plus project detail pane and compact action toolbar.
- Keep import/export and all route actions in toolbar or overflow menu.
- Preserve modal editor for deep edits.

Function coverage confirmation:
- Covers search/filter/hierarchy filtering, new/open project, route navigation, package import/export, and preview/edit handoff.
- Adds TreeView-based project management without losing board data.

## PI-PROJECT-MODAL Project Create/Edit Wizard

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectModalHost.razor`

Current display:
- Dialog wizard with steps `Identity`, `Dates and phases`, `Stack profile`, `Linked objects`, `Review`.
- Supports overview/editor switching, add/remove phases, add/remove starter objects, next/previous, save, save and open structure, delete.

Current UX flows:
- User creates or edits a project, steps through wizard, saves, optionally opens structure immediately.

Target proposal:
- Use `02-project-pages-tabs-dialogs-proposal.png` panel 2.
- Use a dense `InspectorDialogScaffold` style: left step rail, main form, review/context rail, sticky footer.

Function coverage confirmation:
- Covers all wizard steps and footer actions.
- Improves professional clarity by reducing full-page modal sprawl and showing review state consistently.

## PI-PROJECT-HIERARCHY Project Hierarchy Modal

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectHierarchyModal.razor`

Current display:
- Dialog for selecting/opening hierarchy relationships.
- Supports close, project preview, open structure, and opening another hierarchy context.

Current UX flows:
- User browses related projects, selects parent/child context, opens preview or structure.

Target proposal:
- Use `02-project-pages-tabs-dialogs-proposal.png` panel 3.
- TreeView left, selected parent/detail right, footer actions.

Function coverage confirmation:
- Covers hierarchy browse/select/open flows.
- Aligns with the Economy BU tree concept.

## PI-PROJECT-GANTT Mermaid Gantt Preview Dialog

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`

Current display:
- Wide `Dialog` with preview facts, generated Mermaid source, row/dependency badges, and `Structure`/`Close` actions.
- Empty state appears when preview cannot be built.

Current UX flows:
- User opens Gantt preview, reviews source, optionally opens structure.

Target proposal:
- Use `02-project-pages-tabs-dialogs-proposal.png` panel 4.
- Keep split source/rendered preview and compact facts.

Function coverage confirmation:
- Covers Gantt source review, dependency facts, close/open structure, and error state.
- Adds clearer preview layout for customer recording.

## PI-PROJECT-STRUCTURE `/projects/{ProjectId}/structure`

Source references:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureSupportPanels.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureCanvasDialogs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureProcessAssignmentDialog.razor`

Current display:
- Large canvas workbench with toolbar actions, create actions, support panels, TreeView outline, floating windows, inspector panels, validation/health/signals/toolbox windows, dialogs for hierarchy/block/transfer/process/workflow/start/attachment/secret/reference flows.
- Already has a support TreeView in `ProjectStructureSupportPanels`.

Current UX flows:
- User selects/open nodes, creates dependencies, creates nodes, links/unlinks, toggles windows, opens local attachments, previews attachments, starts processes/workflows, assigns agents, validates selected nodes, saves view/window state.

Target proposal:
- Use `02-project-pages-tabs-dialogs-proposal.png` panel 5.
- Preserve canvas-first workspace but reduce shell/header waste; make support TreeView more discoverable and keep inspector/dialog patterns consistent.

Function coverage confirmation:
- Covers canvas, structure outline TreeView, toolbar, inspector, attachment and process/workflow actions.
- Does not propose replacing the workbench; it tightens layout and dialog consistency.

## PI-PROJECT-CALENDAR `/projects/{ProjectId}/calendar`

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectCalendarPage.razor`

Current display:
- Calendar page with summary tiles `Events`, `Linked artifacts`, `Selected item`, `Statuses`.
- Empty states for unavailable project and no selected event.

Current UX flows:
- User opens project calendar, selects event/item, sees linked artifacts and status context.

Target proposal:
- Use `02-project-pages-tabs-dialogs-proposal.png` panel 6.
- Full-width calendar grid, compact summary strip, side event inspector/dialog.

Function coverage confirmation:
- Covers event selection, linked artifacts, status counts, and event detail.
- Improves workspace use for large desktop calendar operations.
