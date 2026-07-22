---
name: candoitall-components-mcp
description: Use whenever a repository consumes any non-WebGL CanDoItAll Components library (BaseLib, CanvasLib, Charts, Common, Mermaid, OverlayLib, or QRCode), especially for component selection, compact task-first page composition, application layout/menu work, setup/assets, sandbox proof, or before adding custom structural CSS.
---

# CanDoItAll Components MCP

## Workflow

1. Treat this MCP as mandatory whenever a repo uses any supported `CanDoItAll.Components.*` package. WebGL libraries are out of scope for this skill and MCP workflow.
2. Call `components_libraries_list` before adding a Components package, service registration, stylesheet, generated asset component, or direct JavaScript import.
3. For page, list, form, card, metric, tab, or dialog work, read [references/compact-ui-composition.md](references/compact-ui-composition.md) before selecting the composition.
4. Call `components_recommend useCase="..."` before choosing components for a page, control, feedback state, overlay, chart, diagram, QR flow, canvas surface, or new composition. Include the primary task, supporting content, density needs, and editor interaction in the use case.
5. For a new or refactored application layout or primary navigation, call `app_shell_guide_get` before writing markup. It is the source of truth for `ThemeHost`, `Layout`, `SideMenu`, `Body`, `PageScaffold`, overlay hosts, scroll ownership, routing, overflow, and responsive behavior.
6. Call `component_get` for every shortlisted component, then `component_usage_examples` to mirror real product or sandbox usage and `component_examples` for a visual proof route.
7. Prefer shared parameters such as `Columns*`, `ColumnTemplate*`, `Gap`, `AlignItems`, `JustifyContent`, variants, sizes, asset components, and service registrations before page-local structural classes or direct library imports.
8. If the shared components cannot express the shape cleanly, improve the relevant shared library or sandbox coverage instead of normalizing a one-off structural wrapper.

## Viewport Scope

- CanDoItAll applications, including sibling application repositories, target large-screen desktop use. Choose and validate components at a maximized or named desktop viewport.
- Do not add or tune small/medium/tablet/mobile application behavior unless the user explicitly requests it.
- Reusable basic components in `CanDoItAll.Components.BaseLib` are the exception: design and validate them for small, medium, and large viewports.
- For CanvasLib, Charts, Mermaid, OverlayLib, QRCode, and other shared libraries, preserve existing responsive behavior when touched; new cross-viewport behavior is separate scope.

## Layout Rules

- Use `ThemeHost` once around the full interactive application.
- Use `Layout` as the app frame. For viewport-owned apps, pair `FitViewport="true"` and `LockOverflow="true"` with exactly one child scroll owner.
- Put `SideMenu` directly inside `Layout` beside `Body`; `SideMenu` is already an aside, so do not wrap it in `Sidebar`.
- Use a stable `SideMenu.MenuId`, route selections through `ItemSelected` or `SideMenuService`, and let the component own its existing overflow and responsive behavior. Do not build a separate application mobile menu without explicit scope.
- Use `Items` for runtime menu models, `MenuItems` for declarative entries, and never duplicate IDs across the composed sources. Put secondary destinations in More and account/settings/help utilities at the bottom.
- Use `PageScaffold` on routed pages, not as a replacement for the application shell. Use `FillHeight` and `FocusWorkbench` only when the inner surface owns overflow.
- Use `Stack` for one-dimensional vertical or horizontal flows.
- Use `Grid` for explicit tracks, section shells, and responsive page composition.
- Use `Row` inside `Grid` when sibling columns should inherit or override tracks and collapse responsively.
- Use `Column` as the content cell and local flex container inside `Row`.
- Use `FormRow` for standard field pairings before building a custom form wrapper.
- Use `SectionHead`, `SectionCard`, and `PageScaffold` when the page matches those semantics.
- Choose badges or `CompactStatStrip` for supporting numbers; use `SummaryTiles` or `StatsGrid` only when metrics are a primary surface or intentionally fill an otherwise sparse page.
- Keep one primary working surface visually dominant. Move independent create/edit flows to a correctly sized `Dialog` and use tabs for mutually exclusive supporting views when that improves task visibility.

## Recommended Tool Sequence

- `components_libraries_list` for package, registration, asset, and style setup.
- `components_recommend useCase="large-screen desktop page with a side navigation and metrics"` to choose a component path.
- `app_shell_guide_get mode="full-height"` for an application shell or primary menu.
- `component_get component="SideMenu"` and the other shortlisted components.
- `component_usage_examples component="SideMenu"` and `component_examples component="SideMenu"` to review real usage and proof routes.
- `component_css_tokens_get` only after structure and component selection are settled and a styling question remains.

## Do Not

- Do not start with raw Tailwind `grid-cols-*`, `flex`, app-shell wrappers, a custom mobile menu, or direct library JavaScript imports when a shared component or generated asset component already expresses the behavior.
- Do not treat the MCP as a static inventory only; pull real usage examples before introducing a new pattern.
- Do not keep layout experiments on product pages when a sandbox route should own them.
- Do not stack an independent editor beneath a list by default, duplicate page introductions, or turn every statistic into a card.
- Do not interpret compact as cramped. Preserve readable type, usable controls, hierarchy, and intentional whitespace.
- Do not use QR, OverlayLib, Charts, Mermaid, CanvasLib, or BaseLib without first checking their service/asset guidance through this MCP.

## Output Expectations

- Name the shared components and library setup you chose and why.
- For page composition, state the primary surface, supporting-content placement, stats treatment, list/editor organization, textarea and dialog sizing, and scroll owner.
- For application shell work, state the chosen scroll owner, menu `MenuId`, routing/selection path, and large-screen desktop proof route.
- For reusable basic BaseLib work, state the small, medium, and large proof routes or viewports.
- For layout-sensitive UI, report what was visible in the first viewport and the findings from screenshots with relevant overlays open.
- If custom CSS is still required, state which shared component path was insufficient and whether BaseLib should be improved.

## Reference

- Treat [references/compact-ui-composition.md](references/compact-ui-composition.md) as the source of truth for compact, task-first page, card, form, metric, tab, dialog, and browser-proof decisions.
