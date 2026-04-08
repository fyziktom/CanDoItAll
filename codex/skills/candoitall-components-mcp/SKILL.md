---
name: candoitall-components-mcp
description: Use when working on CanDoItAll Blazor pages or shared UI that consumes CanDoItAll BaseLib or CanvasLib components, especially for component selection, layout refactors, sandbox proof work, or any task where Codex should query the CanDoItAll components MCP before adding custom structural CSS.
---

# CanDoItAll Components MCP

## Workflow

1. Query `candoitall_components.components_search` or `component_get` before writing new layout markup.
2. For page structure, inspect `Grid`, `Row`, `Column`, `Stack`, `FormRow`, `SectionHead`, `PageScaffold`, and `StatsGrid` first.
3. Call `component_usage_examples` to mirror real usage from `CanDoItAll.Web`, modules, or sandbox pages.
4. Call `component_examples` when you need curated sandbox routes for visual proof.
5. Prefer shared component parameters such as `Columns*`, `ColumnTemplate*`, `Gap`, `AlignItems`, `JustifyContent`, variants, and sizes before page-local structural classes.
6. If the shared components still cannot express the shape cleanly, improve BaseLib or sandbox coverage instead of normalizing a one-off structural wrapper.

## Layout Rules

- Use `Stack` for one-dimensional vertical or horizontal flows.
- Use `Grid` for explicit tracks, section shells, and responsive page composition.
- Use `Row` inside `Grid` when sibling columns should inherit or override tracks and collapse responsively.
- Use `Column` as the content cell and local flex container inside `Row`.
- Use `FormRow` for standard field pairings before building a custom form wrapper.
- Use `SectionHead`, `SectionCard`, `PageScaffold`, `SummaryTiles`, and `StatsGrid` when the page matches those semantics.

## Recommended Tool Sequence

- `components_search query="layout hero"` or search by exact component name.
- `component_get component="Grid"` plus the other likely layout primitives.
- `component_usage_examples component="Grid"` to review real Razor usages.
- `component_examples component="Grid"` to open curated sandbox routes.
- `component_css_tokens_get` only after structure is settled and a styling question still remains.

## Do Not

- Do not start with raw Tailwind `grid-cols-*`, `flex`, or wrapper divs when BaseLib parameters already express the structure.
- Do not treat the MCP as a static inventory only; pull real usage examples before introducing a new pattern.
- Do not keep layout experiments on product pages when a sandbox route should own them.

## Output Expectations

- Name the shared components you chose and why.
- If custom CSS is still required, state which shared component path was insufficient and whether BaseLib should be improved.
