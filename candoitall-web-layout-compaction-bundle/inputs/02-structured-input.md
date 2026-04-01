# Structured Input

## Objectives

- Optimize the CanDoItAll web app for maximized large-screen browser usage before mobile refinement.
- Remove unnecessary first-screen height usage on main routes, especially stacked headers, summaries, filters, and explanatory text.
- Rework the projects board so search, filter selects, and reset fit onto one large-screen toolbar row whenever there is reasonable width.
- Analyze all main pages and modal or overlay surfaces, not only the projects route.
- Improve shared components when local pages are fighting the layout system instead of using it naturally.
- Keep the implementation inside the existing component library and Tailwind-first styling approach.

## Hard Constraints

- Preserve existing page behaviors, navigation, and typed service boundaries.
- Prefer the smallest correct change over route-specific rewrites.
- Use existing BaseLib or app components where possible.
- Prefer Tailwind classes and imported Tailwind modules over ad hoc plain CSS.
- Verify the app in a real browser with a large viewport first, then narrower widths where layout changed materially.
- Keep the bundle explicit enough that another implementation agent could execute it phase by phase.

## Assumptions

- The startup database-selection modal appearing during development is expected in the current workspace and must remain functional, but its open-state layout can still be improved.
- The initial workspace may contain few or no projects, so empty states must be treated as real first-class layouts during validation.
- Prompt factory and workbench surfaces are operationally important enough to justify compacting their custom dialogs and overlay chrome even if their content density differs from list/detail pages.
- The existing Tailwind build pipeline in `Tailwind/input.css` is the intended styling path for component-level class improvements.

## Risks

- Shared shell or scaffold width changes can affect every route at once, so this must be treated as a critical UI foundation.
- Dialog shell changes can easily introduce clipping, scroll regressions, or footer/header compression across unrelated modal flows.
- Prompt factory and project structure use custom overlay systems, so they may not benefit automatically from shared dialog improvements.
- Some pages use raw `InputText` and `InputSelect` directly instead of BaseLib wrappers, so the initiative needs both shared primitives and page-level cleanup.

## Validation Expectations

- Use one managed CanDoItAll watch session and keep validation near the edited surface.
- Keep Tailwind watch running and confirm that edits in imported files under `Tailwind/` rebuild `output.css`.
- Validate large-screen routes first around `1720x1160`, then narrower widths such as `1280x900` and `768x1024` where layout changed.
- Capture real browser screenshots and textual snapshots for critical routes and modal open states.
- Record route, viewport, browser actions, screenshots, and pass/fail decisions in `reviews/01-execution-report.md`.

## Non-Goals

- Redesign the product brand, navigation information architecture, or workbench feature set.
- Replace the current component library with Radzen or raw HTML-only layouts.
- Change repository data models, routing semantics, or non-UI service behavior unless a UI fix depends on a small surface-level adjustment.
