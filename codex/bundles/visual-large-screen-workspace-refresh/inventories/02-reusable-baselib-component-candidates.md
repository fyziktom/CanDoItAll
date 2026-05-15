# Reusable BaseLib Component Candidates

This inventory identifies reusable components implied by the page proposals. Foundation subbundles must build or extend these before page-level repairs, so individual pages do not solve the same layout problems with one-off CSS.

## Existing Components To Reuse Or Extend

| Existing component | Source | Current role | Needed extension |
|---|---|---|---|
| `PageScaffold` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Layout\PageScaffold.razor` | Page container with `Mode`, `MaxWidthClass`, `FillHeight`, header/lead/secondary rail. | Add or standardize large-screen full-width workbench usage and denser spacing presets if current `FocusWorkbench` is not enough. |
| `ListDetailShell` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Lists\ListDetailShell.razor` | Two-pane list/detail shell with dense mode. | Add TreeView-friendly list header/footer slots, status/footer slot, optional unrounded/dense B2B variant, and stable scroll constraints. |
| `TreeView` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\TreeView.razor` | Hierarchical navigation with `Workbench` style and context menu. | Add search/highlight integration guidance, compact footer/status pairing, and typed adapter test patterns. |
| `Tabs` / `SecondaryTabs` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation` | Page and dialog tabs. | Add dense workspace tab presets for many tab bodies, badge-heavy tabs, and large-screen fill-height panels. |
| `Dialog` / `DialogScaffold` / `InspectorDialogLayout` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Modals` | Modal shell and structured dialog body. | Add standard inspector dialog presets for context rail, main form, review panel, validation strip, and sticky footer. |
| `TooltipTarget` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\TooltipTarget.razor` | Hover/focus tooltip trigger. | Use for collapsed rail right-side tooltips and consider richer flyout content/test id conventions. |
| `SummaryTile` / `SummaryTiles` / `MetricCard` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Cards` | Summary counts and metric cards. | Add compact status strip variant with icons/deltas and lower chrome. |
| `Toolbar` / `ToolbarActions` / `FilterBar` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation` | Existing command/filter composition. | Standardize icon-first action toolbar, search/filter/overflow layout, and selection action strip. |
| `EmptyState` / `LoadingState` / `Alert` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback` | Empty/loading/feedback surfaces. | Add compact operational variants for list/detail panes. |

## New Or Extended Generic Patterns

| Pattern | Build as | Reuse targets | Required behavior |
|---|---|---|---|
| `DesktopCommandRail` | New shared shell component or strongly typed `AppShell` composition backed by BaseLib primitives. | App shell. | Collapsed default, expanded state, icon-first items, minimal labels, right-side tooltip slots, bottom utility actions, active route state, keyboard/focus support. |
| `HoverInfoCard` | BaseLib flyout/tooltip composition or component. | DB flyout, entity metadata flyouts, future compact help. | Hover/focus open, safe copy slot, title/status, facts list, action footer, no clipping in large desktop shell. |
| `TreeDetailWorkspace` | Extension/composition around `PageScaffold`, `ListDetailShell`, `TreeView`, `Toolbar`, `SummaryTiles`. | Projects, processes, workflows, plugins, prompt gallery, CRM/HR directory. | Search/filter header, tree pane, selected detail pane, compact metric strip, status footer, empty state. |
| `DenseTabWorkspace` | `Tabs`/`SecondaryTabs` variant plus body frame guidance. | Processes, live processes, workflows, prompt factory, plugins, scheduler, collaboration. | Fill-height desktop tabs, badges, concise tab labels, stable body scroll, no text overlap. |
| `InspectorDialogScaffold` | Extend existing `InspectorDialogLayout`/`DialogScaffold`. | Project wizard, role dialog, workflow dialogs, plugin grants/connections/logs, CRM merge/conversion, scheduler target picker. | Context rail, main content, optional review rail, validation strip, sticky footer, dense form spacing. |
| `MetricStatusStrip` | Extend `SummaryTiles`/`MetricCard`. | Dashboard, live processes, agents, workflows, CRM/HR, scheduler, validation, test lab. | Compact KPI cards with icons/deltas/status tones, no oversized dashboard-card mosaic. |
| `EntityActionToolbar` | Extend `Toolbar`, `ToolbarActions`, `FilterBar`. | Most list/detail pages. | Search, filters, icon buttons, overflow menu, selection action row, stable large-screen dimensions. |

## Foundation Subbundles

- `subbundles/00-02-baselib-desktop-shell-overlay-primitives` owns `DesktopCommandRail` and `HoverInfoCard` style primitives plus shell-specific test hooks.
- `subbundles/00-03-baselib-tree-detail-tab-dialog-primitives` owns `TreeDetailWorkspace`, `DenseTabWorkspace`, `InspectorDialogScaffold`, `MetricStatusStrip`, and `EntityActionToolbar` patterns.

## Reuse Rules

- Do not add new page-local CSS for proposal styling.
- Prefer enum variants, component parameters, `Class` hooks on existing BaseLib components, and Tailwind utility composition.
- Use strongly typed node builders for trees; do not encode page behavior in magic string ids in markup.
- If a page cannot use a generic pattern cleanly, document the exception in the owning page input and execution report.
