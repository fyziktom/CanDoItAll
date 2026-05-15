# Current State

## Repo Observations

- The app shell is `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components\AppShell.razor`, consumed by `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.razor`.
- Navigation items are declared in `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Composition\ShellNavigation.cs` with long descriptions that are currently rendered directly in the sidebar.
- Database status and the `Switch database` action are rendered in `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayoutTopBar.razor`, consuming horizontal page-header space on every route.
- The shell uses a permanent dark large-screen sidebar (`w-72` standard, `w-64` focus) and a right rail that can take `20rem` at `2xl`, which reduces working area on dense pages.
- Shared `TreeView` exists in `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\TreeView.razor` and has `TreeViewStyle.Workbench`, but repo usage is limited to the process template preview and project-structure support panels.
- The Economy reference app uses a compact app nav and a `BusinessUnitTree` that maps typed hierarchy data into `TreeViewNode` rows with icons, badges, selected state, and expand/collapse state.
- No Radzen usage was found by `rg -n "Radzen" C:\repositories\CanDoItAll\src C:\repositories\CanDoItAll\tests -g *.cs -g *.razor -g *.csproj`.

## Screenshot Read

- `reference-02-run-observation-page.png` shows the desired direction: thin icon rail, compact run list, full-width header facts, tabs, KPI strip, small-radius panels, and clear operational status.
- `reference-11-run-bus-tab.png` shows the desired tree/detail behavior: searchable left tree, selected row highlighting, compact status/footer counters, and a large detail pane that keeps related information reachable without dominating the navigation.

## Page Inventory Summary

- The product app exposes 29 route-bearing Razor files under `CanDoItAll.Web` and `CanDoItAll.Modules.*`.
- Most module pages already use `PageScaffold`; `PromptFactoryPage.razor` and thin process route wrappers are notable exceptions.
- Several high-value pages are very large and likely need focused density passes: `PromptFactoryPage.razor` (~3030 lines), `ProjectStructurePage.razor` (~2740 lines), `PluginsPage.razor` (~1306 lines), CRM/HR directory and CRM pages (~1270 lines each), `ProcessWorkspace.razor`, and `LiveProcessesDashboard.razor`.
- Process pages already have dense workbench direction but rely on page-local classes and `ListDetailShell` lists rather than `TreeView`.
- Projects are currently board/card-first and should gain a tree-driven portfolio/relationship view.
- Workflows are under AgentFramework and need tree grouping for definitions, versions, components, and runs.
- Detailed page inputs now describe current elements, current display, UX flows, proposal mapping, and function coverage under `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs`.
- Accepted proposal boards now cover shell, project, process/live, agent/workflow, core admin, supporting modules, and reusable BaseLib components under `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\evidence\design-proposals\pages`.

## Component MCP Findings

- `TreeView` is the correct shared primitive for hierarchical exploration; it supports `Items`, `OnSelect`, `OnToggle`, `OnContextMenu`, `AriaLabel`, and `TreeViewStyle.Workbench`.
- `TooltipTarget` is the correct local hover/focus wrapper for compact icon-only controls and supports right-side placement through `TooltipPosition`.
- `HelpPopover` is available for compact contextual help but should not replace the database flyout because that flyout needs richer state and copy behavior.
- `PageScaffold` already has `Mode`, `FillHeight`, `MaxWidthClass`, and slots that can support full-width desktop workspaces without custom wrapper CSS.
- `Dialog`, `DialogScaffold`, `Tabs`, `SecondaryTabs`, `Grid`, `Stack`, and `ListDetailShell` are available for progressive disclosure and dense workbench layout.
- The component MCP transport was not available during the repair pass, so the current component candidate inventory is grounded in direct source inspection of BaseLib components.
