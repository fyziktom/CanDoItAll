# UI refactoring integration

The `ui-refactoring-v2` integration applies the shared theme, form controls and toolbar
to the current extracted feature renderers. Routed modules continue to own reads,
mutations, pending edits and editor lifetimes as described in [UI component seams](ui-component-seams.md).

## Workspace navigation

`AppShell` retains the open-workspace tab bar and the database/status controls. Tabs
stay in one horizontally scrolling row. The active tab is brought into view on selection;
Left, Right, Home and End select within the row without scrolling the document. The
All tabs menu and search keep long collections accessible. Pinning, ordering, sleeping,
closing and recently closed history continue through the existing `WorkbenchStateService`.
Each tab reserves separate space for its actions menu and close button, so hovering does
not cover its activation target. The shared context menu provides keyboard operation,
outside-click dismissal and focus restoration.
The strip observes its available width and the workbar height. Maximized canvases reserve
the measured workbar, including the project summary, instead of assuming a fixed offset.

The shell propagates its remaining height through the routed body. Ordinary content can
scroll there; fill-height workspaces retain their internal scroll regions and visible
pagers. Application proof targets large desktop viewports only.

## Header and theme ownership

Feature renderers retain their `PageHeader` declarations and their existing dependency
boundaries. The shell supplies BaseLib's optional `IPageHeaderOutlet` cascade only to the
routed body. A header publishes its presentation, actions and statistics to
`AppToolbarState`; the toolbar, help and statistics outlets render independently of the
body. Standalone sandboxes and renderers still render their own headers.

Every registration is tied to its component owner. Disposal of a previous page cannot
clear a newer page's actions, help or statistics. Visibility preferences retain the
toolbar cookie contract across prerender and the interactive circuit.

Shared controls use the new `ButtonTone`, `TabsDensity`, `TextInput`, `NumberInput`,
`SelectInput` and `TextAreaInput` contracts. Native draft-aware controls retain their
pending-input and `EditContext` semantics. Application styles consume shared theme tokens;
reusable control styling stays in Components.
Secondary navigation and shared record cards use the theme's surface, foreground and
border tokens so their selected and inactive states remain legible in both themes.
Process role cards size to their containing pane and their content, keeping long role
names, summaries and action badges separate.

## Agent identity

Bundled people avatars remain the default selection and the first picker gallery.
Robots are an explicit optional gallery backed by bundled SVG assets. Selecting a robot
saves its asset URL through the existing avatar field; clearing it returns to the stable
human default. Existing avatars, uploaded images and generated images retain their
current persistence and ownership behavior.

## Dependency and validation

This change requires the matching Components integration source. The application CI
pins that source revision; existing published packages do not contain the new contracts.
The Components MCP source-mode build describes those same contracts and proof routes.

Local validation uses affected production builds, focused shell/avatar/provider tests,
desktop Playwright MCP journeys and screenshot inspection. The reviewed rendering register
and `portability-static` remain required. Full .NET suites run through GitHub CI.

Desktop proof covers crowded workspace tabs, search and keyboard selection, close/reopen,
pinning and ordering, sidebar expansion, human/robot avatar selection, save/reload and
return to the human default, nested agent dialogs, asynchronous header statistics,
workforce filtering and pagination, project canvas maximize/restore, Gantt scale changes
and a saved task. Prompt list/editor layouts, process role cards and floating chat draft
closure are inspected without sending a provider request. It also checks the
main module routes and their real screenshots at 1920×1080 and 1536×864. Test data belongs
to an isolated PostgreSQL fixture; the ordinary application on 5032 is preserved.
