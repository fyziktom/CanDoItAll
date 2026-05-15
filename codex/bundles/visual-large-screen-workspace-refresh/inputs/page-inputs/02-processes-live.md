# Page Inputs: Processes And Live Operations

## PI-PROCESSES Process Workspace Routes

Source references:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\ProcessesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\ProjectProcessesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`

Current display:
- `/processes` and `/projects/{ProjectId}/processes` are thin wrappers over `ProcessWorkspace`.
- `ProcessWorkspace` uses a dense list/detail shell with a flat definition list, search, new definition, templates, and tabs.
- Main tabs are `Definition`, `Roles`, `Steps`, `Runs`, `Analytics`, `Exchange`, and `Manager chat`.

Current UX flows:
- User searches definitions, creates new definition, opens templates, selects definition, edits definition metadata, saves/publishes, manages roles, steps, runs, analytics, exchange, and manager chat.
- Project-scoped route narrows the process context by active project id.

Target proposal:
- Use `03-process-pages-tabs-dialogs-proposal.png` panel 1.
- Convert flat process definition list into a searchable `TreeView` grouped by global/project scope, status, subprocess relationship, and run health where available.
- Keep selected process detail pane and all existing tab actions.

Function coverage confirmation:
- Covers global/project route wrappers, search, new/template actions, definition selection, and full tab set.
- Adds the required TreeView concept without changing process ownership.

## PI-PROCESS-DEFINITION Definition Tab

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`

Current display:
- Definition metadata form and publication area.
- Buttons include `Save definition`, `Publish`, `Export current definition`, and `Import definition`.

Current UX flows:
- User edits process definition fields, saves draft, publishes, imports/exports definition data.

Target proposal:
- Use `03-process-pages-tabs-dialogs-proposal.png` panel 2.
- Compact two-column definition form with publication facts/actions in a side panel.

Function coverage confirmation:
- Covers metadata edit, save, publish, import, export, and status display.
- Reduces vertical form sprawl.

## PI-PROCESS-ROLES Roles Tab And Role Dialog

Source references:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessTemplateLibraryDialog.razor`

Current display:
- `Roles` tab with `Role requirements` form section.
- Actions include `Add role`, `Templates`, `Details`, `Remove role`, and role dialog `Save role`/`Cancel`.

Current UX flows:
- User adds or edits process roles, applies templates, views role details, removes role.

Target proposal:
- Use `03-process-pages-tabs-dialogs-proposal.png` panel 3.
- Role table/list with compact inline status, and a dense inspector dialog for role edit.

Function coverage confirmation:
- Covers add/edit/remove/apply template/details/save/cancel role flows.
- Uses dialog to keep tab content clearer.

## PI-PROCESS-STEPS Steps Tab And Canvas Action Dialog

Source references:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceStepsTab.razor`

Current display:
- Steps tab hosts definition canvas via `CanvasWorkbenchStage`.
- Actions include start/complete/block/wait/refuse/fail runtime actions where relevant, plus authoring actions `Edit step`, `Add dependent step`, `Add subprocess step`, `Add branch outcome`, `Add role binding`, `Add artifact expectation`, `Remove step`.
- Canvas action dialog opens for selected node/action.

Current UX flows:
- User models process steps visually, selects a step, uses context actions, adds dependencies/subprocesses/branches/bindings/artifacts, removes or edits steps.

Target proposal:
- Use `03-process-pages-tabs-dialogs-proposal.png` panel 4.
- Keep canvas center, tool palette left, selected action dialog right, minimap bottom-right.

Function coverage confirmation:
- Covers definition canvas and all listed authoring actions.
- Moves action detail out of the crowded tab body.

## PI-PROCESS-RUNS Runs Tab And Nested Tabs

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsTab.razor`

Current display:
- Runs tab has nested tab content: `Launch`, `Activity`, `Control`, `Execution`, `Coordination`, `Evidence`.
- Empty states exist for choosing a run before editing coordination, evidence, or controls.

Current UX flows:
- User launches process runs, selects run activity, operates controls, reviews execution/canvas, manages coordination, records artifacts/evidence.

Target proposal:
- Use `03-process-pages-tabs-dialogs-proposal.png` panel 5.
- Nested tab content appears as compact operational sub-panels with selected run summary always visible.

Function coverage confirmation:
- Covers every nested tab and choose-run dependency.
- Keeps run context persistent across sub-tabs.

## PI-PROCESS-ANALYTICS-EXCHANGE-CHAT Analytics, Exchange, Manager Chat, Choose Run Dialog

Source references:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceAnalyticsTab.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.ManagerChat.cs`

Current display:
- Analytics tab shows run analytics and run step drill-ins.
- Exchange tab imports/exports definitions.
- Manager chat tab lets user choose/reload a run, chat, and open runtime details.
- `Select process run` dialog is used by manager chat/runtime flow.

Current UX flows:
- User reviews metrics, clears filters, opens run steps dialog, exports/imports, chooses run, reloads manager chat, opens runtime details.

Target proposal:
- Use `03-process-pages-tabs-dialogs-proposal.png` panel 6.
- Combine compact metric strip, exchange list, chat transcript, and a clean choose-run picker dialog.

Function coverage confirmation:
- Covers analytics, exchange, chat, choose-run, reload, runtime detail actions.
- Keeps secondary details reachable without widening the tab chrome.

## PI-LIVE-PROCESSES Live Process Routes

Source references:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\LiveProcessesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\LiveProcessesDashboard.razor`

Current display:
- `/processes/live` and `/projects/{ProjectId}/processes/live` render `LiveProcessesDashboard`.
- Top tabs include `Activity`, `Agents`, `Graphs`, and `Tool analytics`.
- Run detail tabs include `Overview`, `Steps`, `Artifacts`, and `Timeline`.
- Summary tiles include `Active`, `Blocked`, `Approvals`, `Agents`, `Artifacts`, `Tool calls`, `Cost`, `Time`, `Context`, `Outbox`, `Observed`, and `Steps`.

Current UX flows:
- User monitors live process cards, filters time range, refreshes, opens process details, stage details, artifacts, escalation details, manager chat, and suppress-card dialog.
- User can approve/deny where approval context exists.

Target proposal:
- Use `03-process-pages-tabs-dialogs-proposal.png` panels 7-8.
- Full-width operations dashboard with compact KPI strip, active process table/list, selected run side detail, and detail dialogs grouped by function.

Function coverage confirmation:
- Covers live tabs, summary metrics, refresh/time-range controls, detail dialogs, approval actions, and suppress flow.
- Adds professional operations-screen density similar to the Economy run observation screenshot.
