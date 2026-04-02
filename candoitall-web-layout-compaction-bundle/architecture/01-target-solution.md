# Target Solution

## 1. Shell And Scaffold Width Strategy

- Let the shell use more of the maximized desktop viewport before adding more cards or nested panels.
- Widen the shared shell frame and standard page scaffold enough that list/detail pages and board pages feel proportional to the wide sidebar and optional right rail.
- Keep focus-workbench surfaces effectively full-width.

## 2. Compact Header And Helper-Copy Pattern

- Reuse `PageHeader` and `HelpPopover` rather than inventing a new page-title system.
- Introduce a consistent rule:
  - keep the title and essential status visible
  - move longer explanatory copy behind a small help affordance when it is not needed continuously
- Prefer compact headers on routes where summary tiles, tabs, or board controls already provide context.

## 3. Shared Filter And Toolbar Pattern

- Establish a reusable large-screen toolbar row where:
  - actions can align to the edge
  - search can expand
  - select filters can sit on the same row
  - reset can remain inline
- Let controls stretch by default and allow callers to constrain width with `Class` when needed.
- Apply this pattern first to projects, then to list/detail routes that currently stack filters under list headers.

## 4. Modal And Overlay Footprint Strategy

- Tighten modal header/body/footer padding where the current footprint wastes space.
- Increase useful body area on large screens without creating oversized empty shells.
- Keep shared dialog changes small and complement them with targeted cleanup in:
  - projects modal host
  - hierarchy modal
  - shell database modal
  - prompt factory custom dialogs
  - project structure overlay dialogs
- Validate overlays only in their open state.

## 5. Tailwind-First Styling Strategy

- Prefer edits inside existing Tailwind imports under `Tailwind/`.
- Prefer class composition through component `Class` parameters and existing semantic classes.
- Avoid introducing new isolated `.razor.css` files unless an existing page already depends on them and the change genuinely belongs there.
- Treat Tailwind watch as part of the implementation loop, not as an optional afterthought.

## 6. Proof Strategy

- Use one managed watch session and one browser session per target flow.
- Validate large-screen first, then narrower widths only after the desktop layout is acceptable.
- For each UI-heavy subbundle, record:
  - route
  - viewport
  - browser actions
  - screenshot paths
  - screenshot review answers
  - gate result

