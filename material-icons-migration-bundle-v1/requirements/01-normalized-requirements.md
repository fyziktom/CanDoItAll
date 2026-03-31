# Normalized Requirements

## RQ-001 Local Material Icons Asset Delivery

- Vendor the Google Material Icons assets into solution-owned files and load them through local static web assets.
- Remove the remote Font Awesome stylesheet dependency from `C:/repositories/CanDoItAll/src/CanDoItAll.Web/Components/App.razor`.

## RQ-002 Shared Foundation In The Component Layer

- Put the primary icon render path in the shared component stack, centered on `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib`.
- Replace the current Font Awesome-specific render logic in the shared `Icon` components with Material Icons rendering.

## RQ-003 Full Inventory Before Broad Migration

- Produce an Excel workbook and supporting CSV exports that inventory every identified icon-related surface before wide implementation starts.
- Keep the inventory granular enough to track completed versus remaining work by file, category, and token.

## RQ-004 Cover `Icon.razor` Call Sites And Pure Icon Escapes

- Review every `<Icon>` call site and every raw icon surface that currently bypasses the shared component path through spans, glyph text, inline button text, preview chips, or shorthand badges.
- Replace those surfaces with corresponding Material icons or explicitly mapped Material-compatible tokens.

## RQ-005 Migrate Shared Renderers And CSS Coupling

- Replace Font Awesome rendering in shared `Button`, `Steps`, `Tabs`, treeview, and related CSS hooks.
- Remove or update CSS selectors coupled to `.rz-fa-icon`, `.rz-icon-fallback`, `.cw-toolbar-symbol`, `.cda-shell-nav-icon`, and similar legacy icon wrappers.

## RQ-006 Migrate Application And Module Surfaces

- Carry the migration through shell, page, module, Workbench, Prompt Factory, CanvasLib, and sandbox surfaces that still emit raw icon text or rely on token-to-Font Awesome translation.
- Do not leave mixed icon systems in place after the migration.

## RQ-007 Preserve Usability And Accessibility

- Preserve readable icon sizing, alignment, and visual affordance across buttons, tabs, trees, toolbars, and overlays.
- Preserve icon-only accessibility labels or equivalent screen-reader text anywhere the visible text is removed.

## RQ-008 Validate With Bundle Gates And Real UI Proof

- Pass the prepared-stage bundle validator before implementation proceeds.
- Validate the finished migration with build or test proof plus real browser evidence on the affected route families, including Workbench.
