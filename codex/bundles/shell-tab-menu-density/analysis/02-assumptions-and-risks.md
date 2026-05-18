# Assumptions And Risks

## Working Assumptions

- Large desktop means the existing desktop shell breakpoint (`xl` / 1280px and wider). Browser proof showed the earlier `2xl` assumption left the search row stacked in the actual shell viewport.
- The menu overflow can be based on a deterministic desktop item budget rather than per-pixel JS measurement in this pass.
- The active page should remain visible in the standard menu when possible; overflow still exposes all pages that are not visible.

## Critical Path Risks

- If the sidebar panel is absolutely positioned inside the sidebar, `overflow-hidden` will clip it. The implementation should mirror the fixed-position database flyout pattern.
- If tab-row compaction is applied below large desktop, text/buttons can become cramped. The desktop-only breakpoint is part of the gate.
- If standard routes are height-limited too aggressively, body content can be clipped. Prefer limiting the desktop sidebar height rather than globally locking every standard page body.

## Validation Risks

- Component tests can prove rendering structure but not visual clipping, so browser proof is required.
- Screenshot capture must include the `more_up` open state, not just the closed sidebar.
- Tailwind source changes require regenerating `src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css`.

## Reopen Triggers

- Reopen `01-01-tab-header-density` if browser proof shows search or badges still wrapping below tabs at large desktop.
- Reopen `02-02-sidebar-overflow-continuation-menu` if the continuation panel is clipped, too tall, uses more than three rows, or the sidebar still scrolls internally.
- Reopen the bundle if any route loses access to a navigation item, badge, or active-state indicator.
