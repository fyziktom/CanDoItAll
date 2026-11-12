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

The routed body remains the page scroll owner; focused workbenches keep their own bounded
surface. Application proof targets large desktop viewports only.

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
