# Target Solution

## Visual Thesis

- A quiet enterprise desktop workbench: compact navigation, full-width operational surfaces, tree-first control of large object sets, small-radius panels, restrained status color, and short labels with details available on demand.

## Shell Architecture

- Extend the existing `AppShell` instead of replacing it with a parallel shell.
- Build or extend reusable shell primitives first in SB00-02 so the product shell does not become one-off markup.
- Add typed shell configuration for collapsed/expanded navigation state, likely an enum or option model such as `AppShellNavigationMode` and reusable bottom action models.
- Keep `ShellNavigationItem` strongly typed and split visible label from tooltip/help text so the collapsed rail can show concise icon labels while retaining full descriptions.
- Move Settings and database controls into a bottom shell action area.
- Remove `Switch database` and active DB details from `MainLayoutTopBar`; leave only compact operational status that genuinely belongs in the top workspace strip.
- Implement the database hover/focus card with shared overlay primitives and safe copy behavior. The flyout should show active profile name, provider kind, source/resolution state, runtime lock indicator, descriptor, and a copy button for non-secret profile summary text.

## Reusable Component Architecture

- SB00-03 owns reusable dense workspace patterns before page repairs begin.
- Prefer extending existing BaseLib components: `PageScaffold`, `ListDetailShell`, `TreeView`, `Tabs`, `SecondaryTabs`, `DialogScaffold`, `InspectorDialogLayout`, `SummaryTiles`, `MetricCard`, `Toolbar`, `FilterBar`, `EmptyState`, and `LoadingState`.
- Candidate patterns are `DesktopCommandRail`, `HoverInfoCard`, `TreeDetailWorkspace`, `DenseTabWorkspace`, `InspectorDialogScaffold`, `MetricStatusStrip`, and `EntityActionToolbar`.
- BaseLib components must stay domain-neutral; page modules provide typed adapters and content.
- Component examples/tests should prove large-screen dimensions, dense variants, scroll constraints, and no text overlap before downstream pages depend on them.

## Page Width And Density

- Use `PageScaffold FillHeight="true"` and `MaxWidthClass="max-w-none"` for dense desktop workspaces where full width is needed.
- Prefer `PageScaffoldMode.FocusWorkbench`, `ListDetailShell`, `Grid`, `Stack`, `Tabs`, `SecondaryTabs`, `Dialog`, and `DialogScaffold` before page-local layout wrappers.
- Keep page headers compact. Use summary tiles only when they support decisions; avoid header text that repeats navigation or module descriptions.
- Move secondary explanations, raw details, long metadata, and infrequent actions into dialogs, popovers, or detail panes.
- Use the page-input files as functional contracts: every page/tab/dialog redesign must preserve the current UX flows listed there.

## Tree-Driven Surfaces

- Build typed adapters that convert project/process/workflow models to `TreeViewNode` without leaking stringly-typed command logic into UI event handlers.
- Projects tree grouping: portfolio/root projects, parent/child hierarchy, related party grouping when useful, direct actions in detail pane.
- Processes tree grouping: global vs project-scoped definitions, status, active/blocked/failed run badges, subprocess relationships when available.
- Workflows tree grouping: workflow definitions, versions, lifecycle status, component/runtime groups, and recent runs.
- Keep tree selection explicit and reversible; no hidden side effects in lifecycle hooks.

## Styling Rules

- Do not add new page-local `.razor.css` files for this refresh.
- Do not add arbitrary CSS selectors in product modules for one-off styling.
- Prefer BaseLib component parameters, enum variants, shared Tailwind source under `C:\repositories\CanDoItAll\Tailwind`, and component `Class` parameters.
- If a shared component cannot express the target layout cleanly, improve the shared component or add a reusable variant with sandbox coverage.

## Validation Architecture

- Browser proof is large-screen first and route-specific.
- `imagegen` output is planning evidence only. Real validation is Playwright browser proof plus screenshot review from the actual Blazor app.
- Accepted proposal boards under `evidence/design-proposals/pages` must be checked against real screenshots during execution; regenerate or tighten instructions when a proposal misses a real function.
- Every changed overlay must have open-state proof for readable content, no clipping, correct layering, and no harmful lateral overflow.
